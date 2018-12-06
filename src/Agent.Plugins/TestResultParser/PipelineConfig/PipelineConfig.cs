using System;

namespace Agent.Plugins.TestResultParser
{
    public class PipelineConfig
    {
        public Guid Project { get; set; }
        public int BuildId { get; set; }
    }
}
