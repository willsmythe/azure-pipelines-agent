using System.Threading.Tasks;
using Agent.Plugins.TestResultParser.Client;

namespace Agent.Plugins.TestResultParser.Gateway
{
    interface ILogParserGateway
    {
        /* Register all parsers which needs to parse the task console stream */
        void Initialize(ClientFactory clientFactory, PipelineConfig pipelineConfig);

        /* Process the task output data */
        Task ProcessDataAsync(string data);

        void Complete();
    }
}
