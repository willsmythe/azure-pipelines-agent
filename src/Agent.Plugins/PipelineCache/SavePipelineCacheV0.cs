using System;
using System.Threading;
using System.Threading.Tasks;
using Agent.Sdk;
using Microsoft.TeamFoundation.DistributedTask.WebApi;

namespace Agent.Plugins.PipelineCache
{    
    public class SavePipelineCacheV0 : PipelineCacheTaskPluginBase
    {
        public override string Stage => "post";

        protected override async Task ProcessCommandInternalAsync(
            AgentTaskPluginExecutionContext context, 
            string path, 
            string keyStr,
            string salt,
            CancellationToken token)
        {
            TaskResult? jobStatus = null;
            if (context.Variables.TryGetValue("agent.jobstatus", out VariableValue jobStatusVar))
            {
                if (Enum.TryParse<TaskResult>(jobStatusVar?.Value ?? string.Empty, true, out TaskResult result))
                {
                    jobStatus = result;
                }
            }

            if (!TaskResult.Succeeded.Equals(jobStatus))
            {
                context.Warning($"Skipping because the job status was not 'Succeeded'.");
                return;
            }

            string[] key = keyStr.Split(
                new[] { '\n' },
                StringSplitOptions.RemoveEmptyEntries
            );
            PipelineCacheServer server = new PipelineCacheServer();
            await server.UploadAsync(
                context,
                key, 
                path,
                salt,
                token);
        }
    }
}