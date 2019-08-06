using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Agent.Sdk;
using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.PipelineCache.WebApi;

namespace Agent.Plugins.PipelineCache
{
    public abstract class PipelineCacheTaskPluginBase : IAgentTaskPlugin
    {
        protected const string RestoreStepRanVariableName = "RESTORE_STEP_RAN";
        protected const string RestoreStepRanVariableValue = "true";

        private const string SaltVariableName = "AZDEVOPS_PIPELINECACHE_SALT";
        private const string OldKeyFormatMessage = "'key' format is changing to a single line: https://aka.ms/pipeline-caching-docs";

        public Guid Id => PipelineCachePluginConstants.CacheTaskId;

        public abstract String Stage { get; }

        internal static (bool isOldFormat, string[] keySegments, IEnumerable<string[]> restoreKeys) ParseKeys(string salt, string key, string restoreKeysBlock)
        {
            Func<string,string[]> splitAcrossPipes = (s) => {
                var segments = s.Split(new [] {'|'},StringSplitOptions.RemoveEmptyEntries).Select(segment => segment.Trim());
                if(!string.IsNullOrWhiteSpace(salt))
                {
                    segments = (new [] { $"{SaltVariableName}={salt}"}).Concat(segments);
                }
                return segments.ToArray();
            };

            Func<string,string[]> splitAcrossNewlines = (s) => 
                s.Replace("\r\n", "\n") //normalize newlines
                 .Split(new [] {'\n'}, StringSplitOptions.RemoveEmptyEntries)
                 .Select(line => line.Trim())
                 .ToArray();
            
            string[] keySegments;
            bool isOldFormat = key.Contains('\n');
            
            IEnumerable<string[]> restoreKeys;
            bool hasRestoreKeys = !string.IsNullOrWhiteSpace(restoreKeysBlock);

            if (isOldFormat && hasRestoreKeys)
            {
                throw new ArgumentException(OldKeyFormatMessage);
            }
            
            if (isOldFormat)
            {
                keySegments = splitAcrossNewlines(key);
            }
            else
            {
                keySegments = splitAcrossPipes(key);
            }
            

            if (hasRestoreKeys)
            {
                restoreKeys = splitAcrossNewlines(restoreKeysBlock).Select(restoreKey => splitAcrossPipes(restoreKey));
            }
            else
            {
                restoreKeys = Enumerable.Empty<string[]>();
            }

            return (isOldFormat, keySegments, restoreKeys);
        }
                
        public async Task RunAsync(AgentTaskPluginExecutionContext context, CancellationToken token)
        {
            ArgUtil.NotNull(context, nameof(context));

            string salt = context.Variables.GetValueOrDefault(SaltVariableName)?.Value;
            string keyInput = context.GetInput(PipelineCacheTaskPluginConstants.Key, required: true);
            string restoreKeysInput = context.GetInput(PipelineCacheTaskPluginConstants.RestoreKeys, required: false);

            (bool isOldFormat, string[] key, IEnumerable<string[]> restoreKeys) = ParseKeys(salt, keyInput, restoreKeysInput);

            if (isOldFormat)
            {
                context.Warning(OldKeyFormatMessage);
            }

            string workspaceRoot = context.Variables.GetValueOrDefault("pipeline.workspace")?.Value;

            Func<string[], bool, Fingerprint> keySegmentsResolver = (keySegments, appendWildcard) => {
                context.Output($"Resolving key: {string.Join(" | ", keySegments)}");
                Fingerprint fingerprint = FingerprintCreator.CreateFromKey(context, keySegments, workspaceRoot);
                fingerprint.Segments = fingerprint.Segments.Concat(new [] { Fingerprint.Wildcard} ).ToArray();
                context.Output($"Resolved to: {fingerprint}");
                return fingerprint;
            };

            Func<Fingerprint> keyResolver = () => keySegmentsResolver(key, false);
            Func<Fingerprint[]> restoreKeysResolver = () => restoreKeys.Select(restoreKey => keySegmentsResolver(restoreKey, true)).ToArray();

            // TODO: Translate path from container to host (Ting)
            string path = context.GetInput(PipelineCacheTaskPluginConstants.Path, required: true);

            await ProcessCommandInternalAsync(
                context,
                keyResolver,
                restoreKeysResolver,
                path,
                token);
        }

        // Process the command with preprocessed arguments.
        protected abstract Task ProcessCommandInternalAsync(
            AgentTaskPluginExecutionContext context,
            Func<Fingerprint> keyResolver,
            Func<Fingerprint[]> restoreKeysResolver,
            string path,
            CancellationToken token);

        // Properties set by tasks
        protected static class PipelineCacheTaskPluginConstants
        {
            public static readonly string Key = "key"; // this needs to match the input in the task.
            public static readonly string RestoreKeys = "restoreKeys";
            public static readonly string Path = "path";
            public static readonly string PipelineId = "pipelineId";
            public static readonly string CacheHitVariable = "cacheHitVar";
            public static readonly string Salt = "salt";

        }
    }
}