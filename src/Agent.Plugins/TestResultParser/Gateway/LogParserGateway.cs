using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Agent.Plugins.TestResultParser.Bus;
using Agent.Plugins.TestResultParser.Client;
using Agent.Plugins.TestResultParser.Loggers;
using Agent.Plugins.TestResultParser.Parser;
using Agent.Plugins.TestResultParser.Publish;
using Agent.Plugins.TestResultParser.Telemetry;
using Agent.Plugins.TestResultParser.TestRunManger;

namespace Agent.Plugins.TestResultParser.Gateway
{
    public class LogParserGateway : ILogParserGateway, IBus<LogData>
    {
        public void Initialize(ClientFactory clientFactory, PipelineConfig pipelineConfig)
        {
            var testRunManager = new TestRunManager(new PipelineTestRunPublisher(clientFactory, pipelineConfig));
            var telemetry = new TelemetryDataCollector(clientFactory);
            var logger = TraceLogger.Instance;

            var parsers = ParserFactory.GetTestResultParsers(testRunManager);
            foreach (var parser in parsers)
            {
                parser.SetTelemetryCollector(telemetry);
                parser.SetTraceLogger(logger);

                Subscribe(parser.Parse);
            }
        }

        public async Task ProcessDataAsync(string data)
        {
            var logData = new LogData
            {
                Message = data,
                LineNumber = ++_counter
            };

            await _broadcast.SendAsync(logData);
        }

        public void Complete()
        {
            try
            {
                _broadcast.Complete();
                Task.WaitAll(_subscribers.Values.Select(x => x.Completion).ToArray());
            }
            catch (Exception)
            {
                // Log it and proceed
            }
        }

        //TODO evaluate ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 2 }
        public Guid Subscribe(Action<LogData> handlerAction)
        {
            var handler = new ActionBlock<LogData>(handlerAction);

            _broadcast.LinkTo(handler, new DataflowLinkOptions { PropagateCompletion = true });

            return AddSubscription(handler);
        }

        public void Unsubscribe(Guid subscriptionId)
        {
            if (_subscribers.TryRemove(subscriptionId, out var subscription))
            {
                subscription.Complete();
            }
        }

        private Guid AddSubscription(ITargetBlock<LogData> subscription)
        {
            var subscriptionId = Guid.NewGuid();
            _subscribers.TryAdd(subscriptionId, subscription);
            return subscriptionId;
        }

        private readonly BroadcastBlock<LogData> _broadcast = new BroadcastBlock<LogData>(message => message);
        private readonly ConcurrentDictionary<Guid, ITargetBlock<LogData>> _subscribers = new ConcurrentDictionary<Guid, ITargetBlock<LogData>>();
        private int _counter;
    }
}
