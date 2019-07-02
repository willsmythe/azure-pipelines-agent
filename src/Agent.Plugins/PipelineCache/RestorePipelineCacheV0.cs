using System;
using System.Threading;
using System.Threading.Tasks;
using Agent.Sdk;

namespace Agent.Plugins.PipelineCache
{    
    public class RestorePipelineCacheV0 : PipelineCacheTaskPluginBase
    {
        public override string Stage => "main";

        protected override async Task ProcessCommandInternalAsync(
            AgentTaskPluginExecutionContext context, 
            string path, 
            string keyStr,
            string salt,
            CancellationToken token)
        {
            string[] key = keyStr.Split(
                new[] { '\n' },
                StringSplitOptions.RemoveEmptyEntries
            );

            PipelineCacheServer server = new PipelineCacheServer();
            await server.DownloadAsync(
                context, 
                key, 
                path,
                salt,
                context.GetInput(PipelineCacheTaskPluginConstants.CacheHitVariable, required: false),
                token);
        }
    }
}