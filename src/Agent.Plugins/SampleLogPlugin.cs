using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Agent.Sdk;
using Pipelines = Microsoft.TeamFoundation.DistributedTask.Pipelines;
using Microsoft.VisualStudio.Services.Agent.Util;
using System.IO;
using Microsoft.VisualStudio.Services.Common;

namespace Agent.Plugins.Log
{
    public class SampleLogPlugin : IAgentLogPlugin
    {
        public string FriendlyName => "Re-save Log";

        private string _fileName = $"{Guid.NewGuid().ToString("N")}.log";

        public Task<bool> InitializeAsync(IAgentLogPluginContext context)
        {
            return Task.FromResult(true);
        }

        public Task ProcessLineAsync(IAgentLogPluginContext context, Pipelines.TaskStepDefinitionReference step, string output)
        {
            context.Trace("DEBUG_PROCESS");
            var file = Path.Combine(context.Variables.GetValueOrDefault("agent.homedirectory").Value, "_diag", _fileName);
            context.Output($"{step.Name}: {output}");
            return Task.CompletedTask;
            //await File.AppendAllLinesAsync(file, new List<string>() { output });
        }

        public async Task FinalizeAsync(IAgentLogPluginContext context)
        {
            context.Trace("DEBUG_FINISH");
            var file = Path.Combine(context.Variables.GetValueOrDefault("agent.homedirectory").Value, "_diag", _fileName);
            await File.AppendAllTextAsync(file, StringUtil.ConvertToJson(context.Variables));
        }
    }
}
