using System.Collections.Generic;
using NreOJPService;

namespace Huxley2.Models
{
    public class OjpJourney
    {
        public int Id { get; set; }

        public CrsStation? OriginStation { get; set; }

        public CrsStation? DestinationStation { get; set; }

        public string? RealTimeClassification { get; set; }

        public JourneyTimetable? JourneyTimetable { get; set; }

        public IEnumerable<OjpLeg> OjpLegs { get; set; } = new List<OjpLeg>();

        public IEnumerable<Fare> Fare { get; set; } = new List<Fare>();

        public IEnumerable<ServiceBulletin> ServiceBulletins { get; set; } = new List<ServiceBulletin>();
    }
}
