using OpenLDBWS;

namespace Huxley2.Models
{
    public class NearbyStationResponse
    {
        public string StationName { get; set; } = string.Empty;

        public string CrsCode { get; set; } = string.Empty;

        /// <summary>
        /// Distance from the provided GPS coordinates in miles, rounded to 1 decimal place.
        /// </summary>
        public double DistanceMiles { get; set; }

        /// <summary>
        /// Live services at this station, matching the existing Real-Time API response format.
        /// When expand=true, items are ServiceItemWithCallingPoints with calling point data.
        /// </summary>
        public object[] Services { get; set; } = System.Array.Empty<object>();
    }
}
