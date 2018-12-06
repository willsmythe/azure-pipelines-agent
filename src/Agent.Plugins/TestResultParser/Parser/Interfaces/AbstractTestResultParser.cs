using Agent.Plugins.TestResultParser.Loggers;
using Agent.Plugins.TestResultParser.Telemetry;
using Agent.Plugins.TestResultParser.TestRunManger;

namespace Agent.Plugins.TestResultParser.Parser
{
    public abstract class AbstractTestResultParser : ITestResultParser
    {
        protected ITestRunManager TestRunManager;
        protected ITraceLogger Logger;
        protected ITelemetryDataCollector Telemetry;

        protected AbstractTestResultParser(ITestRunManager testRunManager)
        {
            TestRunManager = testRunManager;
        }

        public void SetTraceLogger(ITraceLogger traceLogger)
        {
            Logger = traceLogger;
        }

        public void SetTelemetryCollector(ITelemetryDataCollector telemetryDataCollector)
        {
            Telemetry = telemetryDataCollector;
        }

        public abstract void Parse(LogData line);
        public abstract string Name { get; }
        public abstract string Version { get; }
    }
}
