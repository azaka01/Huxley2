using System;
using System.Collections.Generic;
using System.Linq;
using Huxley2.Interfaces;
using Huxley2.Models;
using Huxley2.Utils;

namespace Huxley2.Services
{
    public class NearbyStationService : INearbyStationService
    {
        private readonly IStationService _stationService;

        public NearbyStationService(IStationService stationService)
        {
            _stationService = stationService;
        }

        public List<NearbyStationResult> FindNearbyStations(double lat, double lng, double radiusMiles = 3, int maxResults = 5)
        {
            var allStations = _stationService.GetStations(null);

            return allStations
                .Select(station => new
                {
                    Station = station,
                    Distance = HaversineDistance.CalculateMiles(lat, lng, station.Latitude, station.Longitude)
                })
                .Where(x => x.Distance <= radiusMiles)
                .OrderBy(x => x.Distance)
                .Take(maxResults)
                .Select(x => new NearbyStationResult
                {
                    StationName = x.Station.StationName,
                    CrsCode = x.Station.CrsCode,
                    Latitude = x.Station.Latitude,
                    Longitude = x.Station.Longitude,
                    DistanceMiles = Math.Round(x.Distance, 1)
                })
                .ToList();
        }
    }
}
