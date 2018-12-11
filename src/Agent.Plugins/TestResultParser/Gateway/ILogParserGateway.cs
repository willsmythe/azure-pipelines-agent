using System.Threading.Tasks;
using Agent.Plugins.Log.TestResultParser.Contracts;

namespace Agent.Plugins.TestResultParser.Plugin
{
    public interface ILogParserGateway
    {
        /* Register all parsers which needs to parse the task console stream */
        void Initialize(IClientFactory clientFactory, IPipelineConfig pipelineConfig, ITraceLogger traceLogger);

        /* Process the task output data */
        Task ProcessDataAsync(string data);

        void Complete();
    }
}
