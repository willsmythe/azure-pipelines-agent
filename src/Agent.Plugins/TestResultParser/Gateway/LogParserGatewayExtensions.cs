using System;
using System.Threading.Tasks;
using Agent.Plugins.TestResultParser.Gateway;
using Agent.Plugins.TestResultParser.Parser;

namespace Agent.Plugins.TestResultParser.Bus
{
    public static class LogParserGatewayExtensions
    {
        public static Task SendAsync(this LogParserGateway bus, string message)
        {
            return bus.ProcessDataAsync(message);
        }

        public static Guid Subscribe(this LogParserGateway bus, Func<Action<LogData>> handlerActionFactory)
        {
            return bus.Subscribe(message => handlerActionFactory().Invoke(message));
        }

        public static Guid Subscribe<THandler>(this LogParserGateway bus) where THandler : ITestResultParser, new()
        {
            return bus.Subscribe(message => new THandler().Parse(message));
        }
    }
}
