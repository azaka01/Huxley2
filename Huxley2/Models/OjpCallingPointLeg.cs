using NreOJPService;
using System;
using System.Collections.Generic;

namespace Huxley2.Models
{
    public class OjpCallingPointLeg
    {
        public int Id { get; set; }
        public CrsStation? OriginStation { get; set; }

        public CrsStation? DestinationStation { get; set; }

        public DateTime ArrivalTime { get; set; }

        public DateTime DepartureTime { get; set; }

        public CallingPointsSearchStatus SearchStatus { get; set; }

        public IEnumerable<OjpSingleCallingPointLeg> OjpSingleCpLegs { get; set; } = new List<OjpSingleCallingPointLeg>();

    }
}
