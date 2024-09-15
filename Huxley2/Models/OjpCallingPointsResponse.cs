using NreOJPService;
using System;
using System.Collections.Generic;

namespace Huxley2.Models
{
    public class OjpCallingPointsResponse
    {
        public DateTime GeneratedAt { get; set; }

        public CrsStation? OriginStation { get; set; }

        public CrsStation? DestinationStation { get; set; }

        public IEnumerable<OjpCallingPointLeg> OjpCpLegs { get; set; } = new List<OjpCallingPointLeg>();
    }
}
