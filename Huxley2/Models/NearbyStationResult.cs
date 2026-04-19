namespace Huxley2.Models
{
    public class NearbyStationResult
    {
        public string StationName { get; set; } = string.Empty;
        public string CrsCode { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double DistanceMiles { get; set; }
    }
}
