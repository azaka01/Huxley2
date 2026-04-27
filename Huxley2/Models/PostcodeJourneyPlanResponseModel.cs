using System;
using System.Collections.Generic;

namespace Huxley2.Models
{
    public class PostcodeJourneyPlanResponseModel
    {
        public DateTime GeneratedAt { get; set; }

        public DateTime PlannedTime { get; set; }

        public string Postcode { get; set; } = string.Empty;

        public string StationCrs { get; set; } = string.Empty;

        public IEnumerable<PostcodeStationModel> PostcodeStations { get; set; } = new List<PostcodeStationModel>();

        public IEnumerable<PostcodeJourneyGroup> OutwardJourneys { get; set; } = new List<PostcodeJourneyGroup>();
    }

    public class PostcodeStationModel
    {
        public string Crs { get; set; } = string.Empty;

        public string StationName { get; set; } = string.Empty;

        public double DistanceMiles { get; set; }
    }

    public class PostcodeJourneyGroup
    {
        public string SelectedStation { get; set; } = string.Empty;

        public IEnumerable<OjpJourney> Journeys { get; set; } = new List<OjpJourney>();
    }
}
