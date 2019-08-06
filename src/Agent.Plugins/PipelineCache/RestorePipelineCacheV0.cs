using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agent.Sdk;
using Microsoft.VisualStudio.Services.PipelineCache.WebApi;

namespace Agent.Plugins.PipelineCache
{    
    public class RestorePipelineCacheV0 : PipelineCacheTaskPluginBase
    {
        public override string Stage => "main";

        protected override async Task ProcessCommandInternalAsync(
            AgentTaskPluginExecutionContext context,
            Func<Fingerprint> keyResolver,
            Func<Fingerprint[]> restoreKeysResolver,
            string path,
            CancellationToken token)
        {
            context.SetTaskVariable(RestoreStepRanVariableName, RestoreStepRanVariableValue);

            PipelineCacheServer server = new PipelineCacheServer();
            await server.DownloadAsync(
                context, 
                (new [] { keyResolver() }).Concat(restoreKeysResolver()),
                path,
                context.GetInput(PipelineCacheTaskPluginConstants.CacheHitVariable, required: false),
                token);
        }
    }
}