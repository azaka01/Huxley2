using System.Collections.Generic;

namespace Huxley2.Models
{
    public class NearbyServicesResponse
    {
        /// <summary>
        /// ISO 8601 timestamp indicating when the response data was assembled.
        /// </summary>
        public string GeneratedAt { get; set; } = string.Empty;

        /// <summary>
        /// Array of nearby stations with their services, ordered by distance (nearest first).
        /// </summary>
        public List<NearbyStationResponse> Stations { get; set; } = new List<NearbyStationResponse>();
    }
}
