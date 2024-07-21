using NreOJPService;
using System;
using System.Collections.Generic;

namespace Huxley2.Models
{
    public class OjpResponse
    {
        public DateTime GeneratedAt { get; set; }

        public DateTime PlannedTime { get; set; }

        public CrsStation? OriginStation { get; set; }

        public CrsStation? DestinationStation { get; set; }

        public int ItemChoiceType { get; set; }

        public IEnumerable<OjpJourney> OutwardJourneys { get; set; } = new List<OjpJourney>();

        public IEnumerable<OjpJourney> InwardJourneys { get; set; } = new List<OjpJourney>();

        public NrsStatus? NrsStatus { get; set; }

        public ResponseEnum Response { get; set; }

        public string? ResponseDetails { get; set; }
    }
}
