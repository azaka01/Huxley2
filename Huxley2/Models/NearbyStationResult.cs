namespace Huxley2.Models
{
    public class NearbyStationResult
    {
        public string StationName { get; set; } = string.Empty;
        public string CrsCode { get; set; } = string.Empty;
        public double DistanceMiles { get; set; }
    }
}
