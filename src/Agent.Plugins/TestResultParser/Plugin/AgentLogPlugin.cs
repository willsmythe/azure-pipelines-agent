using System;
using System.Threading.Tasks;
using Agent.Plugins.TestResultParser;
using Agent.Plugins.TestResultParser.Client;
using Agent.Plugins.TestResultParser.Gateway;
using Agent.Sdk;
using Pipelines = Microsoft.TeamFoundation.DistributedTask.Pipelines;

namespace Agent.Plugins.Log
{
    public class TestResultLogPlugin : IAgentLogPlugin
    {
        public string FriendlyName => "Test Result Log Parser";

        public void Initialize(IAgentLogPluginContext context)
        {
            CheckForPluginDisable(context); // throw an exception and if initialization fails, disable plugin?
            PopulatePipelineConfig(context);

            _clientFactory = new ClientFactory(context.VssConnection);
            _inputDataParser = new LogParserGateway();

            _inputDataParser.Initialize(_clientFactory, pipelineConfig: _pipelineConfig);
        }

        public Task ProcessLineAsync(IAgentLogPluginContext context, Pipelines.TaskStepDefinitionReference step, string line)
        {
            return _inputDataParser.ProcessDataAsync(line);
        }

        public Task FinalizeAsync(IAgentLogPluginContext context)
        {
            _inputDataParser.Complete();
            return Task.CompletedTask;
        }

        private void CheckForPluginDisable(IAgentLogPluginContext context)
        {
            // check for PTR task or some other tasks to enable/disable
        }

        private void PopulatePipelineConfig(IAgentLogPluginContext context)
        {
            if (context.Variables.TryGetValue("system.teamProjectId", out var projectGuid))
            {
                _pipelineConfig.Project = new Guid(projectGuid.Value);
            }

            if (context.Variables.TryGetValue("build.buildId", out var buildId))
            {
                _pipelineConfig.BuildId = int.Parse(buildId.Value);
            }
        }

        private LogParserGateway _inputDataParser;
        private ClientFactory _clientFactory;
        private readonly PipelineConfig _pipelineConfig = new PipelineConfig();
    }
}
