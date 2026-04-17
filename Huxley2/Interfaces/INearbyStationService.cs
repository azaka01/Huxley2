using System.Collections.Generic;
using Huxley2.Models;

namespace Huxley2.Interfaces
{
    public interface INearbyStationService
    {
        List<NearbyStationResult> FindNearbyStations(double lat, double lng, double radiusMiles = 3, int maxResults = 5);
    }
}
