using System;

namespace RepositoryAnaltyicsOrchestrator
{
    public class RunTimeConfiguration
    {
        public string Url { get; set; }
        public string User { get; set; }
        public string Organization { get; set; }
        public int BatchSize { get; set; } = 50;
        public int Concurrency { get; set; } = 1;
        public DateTime? AsOf { get; set; }
        public bool RefreshAll { get; set; }
        public int InitialDelayDuration { get; set; } = 0;
        public int FirstApiCallTimeout { get; set; } = 300000;
    }
}
