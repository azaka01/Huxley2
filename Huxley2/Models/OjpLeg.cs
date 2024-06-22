using NreOJPService;
using System;
using System.Collections.Generic;

namespace Huxley2.Models
{
    public class OjpLeg
    {
        public int Id { get; set; }

        public CrsStation? BoardStation { get; set; }

        public CrsStation? AlightStation { get; set; }

        public IEnumerable<string> Origins { get; set; } = new List<String>();

        public IEnumerable<string> Destinations { get; set; } = new List<String>();

        public string? OriginPlatform { get; set; } = string.Empty;

        public string? DestinationPlatform { get; set; } = string.Empty;

        public RealtimeClassification? RealTimeClassification { get; set; }

        public TravelModeEnum? Mode { get; set; }

        public OperatorDetails? OperatorDetails { get; set; }

        public JourneyLegTimetable? JourneyTimetable { get; set; }

        public IEnumerable<string> UndergroundTravelInformation { get; set; } = new List<String>();

    }
}
