using System;
using System.Collections.Generic;
using System.Text;

namespace RepositoryAnaltyicsOrchestrator
{
    public class RunTimeConfiguration
    {
        public string Url { get; set; }
        public string User { get; set; }
        public string Organization { get; set; }
        public int BatchSize { get; set; }
        public int Concurrency { get; set; }
        public DateTime? AsOf { get; set; }
        public bool RefreshAll { get; set; }
        public int InitialDelayDuration { get; set; }
    }
}
