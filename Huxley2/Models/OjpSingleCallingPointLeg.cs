using System;

namespace Huxley2.Models
{
    public class OjpSingleCallingPointLeg
    {
        public CrsStation? Station { get; set; }

        public bool Board { get; set; }

        public bool BoardSpecified { get; set; }

        public bool Alight { get; set; }

        public bool AlightSpecified { get; set; }   

        public string? Platform { get; set; }

        public DateTime? ArrivalTime { get; set; }

        public DateTime? DepartureTime { get; set; }

        public OjpCallingPointDelayOrCancelData? StationDelayOrCancelData { get; set; }
    }
}
