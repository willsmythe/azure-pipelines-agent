using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Agent.Plugins.Log.TestResultParser.Contracts;
using Agent.Plugins.Log.TestResultParser.Plugin;

namespace Agent.Plugins.TestResultParser.Plugin
{

    public class LogParserGateway : ILogParserGateway, IBus<LogData>
    {
        public void Initialize(IClientFactory clientFactory, IPipelineConfig pipelineConfig, ITraceLogger traceLogger)
        {
            _logger = traceLogger;
            var publisher = new PipelineTestRunPublisher(clientFactory, pipelineConfig);
            var telemetry = new TelemetryDataCollector(clientFactory);
            var testRunManager = new TestRunManager(publisher, _logger);
            var parsers = ParserFactory.GetTestResultParsers(testRunManager, traceLogger, telemetry);

            foreach (var parser in parsers)
            {
                //Subscribe parsers to Pub-Sub model
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
            catch (Exception ex)
            {
                _logger?.Warning($"Failed to finish the complete operation: {ex.StackTrace}");
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
        private ITraceLogger _logger;
    }
}
