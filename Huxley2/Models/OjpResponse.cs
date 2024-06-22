using NreOJPService;
using System;
using System.Collections.Generic;

namespace Huxley2.Models
{
    public class OjpResponse
    {
        public DateTime GeneratedAt { get; set; }

        public IEnumerable<OjpJourney> OutwardJourneys { get; set; } = new List<OjpJourney>();

        public IEnumerable<OjpJourney> InwardJourneys { get; set; } = new List<OjpJourney>();

        public NrsStatus? NrsStatus { get; set; }

        public ResponseEnum Response { get; set; }

        public string? ResponseDetails { get; set; }
    }
}
