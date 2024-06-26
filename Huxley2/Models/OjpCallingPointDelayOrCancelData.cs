using System;

namespace Huxley2.Models
{
    public class OjpCallingPointDelayOrCancelData
    {
        public DateTime? UpdatedArrivalTime { get; set; }

        public DateTime? UpdatedDepartureTime { get; set; }

        public bool IsCancelled { get; set; }

        public string? CancellationReason { get; set; }

        public string? DelayedReason { get; set; }
    }
}
