using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using CsvHelper;
using GeoUK;
using GeoUK.Coordinates;
using GeoUK.Ellipsoids;
using GeoUK.Projections;
using Huxley2.Extensions;
using Huxley2.Interfaces;
using Huxley2.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Convert = GeoUK.Convert;

namespace Huxley2.Services
{
    public class CrsStationService : IStationService
    {
        private readonly ILogger<CrsStationService> _logger;
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;

        private volatile IReadOnlyList<CrsStation> _stations = Array.Empty<CrsStation>();
        private volatile IReadOnlyList<CrsStation> _londonTerminals = Array.Empty<CrsStation>();
        private volatile bool _isReady;

        public bool IsReady => _isReady;

        public CrsStationService(
            ILogger<CrsStationService> logger,
            IConfiguration config,
            HttpClient httpClient)
        {
            _logger = logger;
            _config = config;
            _httpClient = httpClient;
        }

        public IEnumerable<CrsStation> GetLondonTerminals() => _londonTerminals;

        public async Task LoadStations()
        {
            // Build locally first; publish atomically at the end.
            var csvStations = await LoadFromCsvAsync().ConfigureAwait(false);
            var jsonAddendum = await LoadFromJsonAddendumAsync().ConfigureAwait(false); // optional

            var merged = MergeStations(csvStations, jsonAddendum);
            var terminals = ComputeLondonTerminals(merged);

            _stations = merged;
            _londonTerminals = terminals;
            _isReady = true;

            _logger.LogInformation("Stations loaded. Count={Count}, LondonTerminals={TerminalsCount}",
                _stations.Count, _londonTerminals.Count);
        }

        public CrsStation? GetStationByCrsCode(string? query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return null;

            var stations = _stations; // snapshot
            var match = stations.FirstOrDefault(c =>
                string.Equals(c.CrsCode, query, StringComparison.OrdinalIgnoreCase));

            if (match == null)
                _logger.LogError("Unable to locate CRS {Crs}", query);

            return match == null
                ? null
                : new CrsStation
                {
                    CrsCode = match.CrsCode,
                    StationName = match.StationName,
                    Latitude = match.Latitude,
                    Longitude = match.Longitude
                };
        }

        public IEnumerable<CrsStation> GetStations(string? query)
        {
            var stations = _stations; // snapshot

            if (string.IsNullOrWhiteSpace(query))
            {
                return stations
                    .Select(c => new CrsStation
                    {
                        CrsCode = c.CrsCode,
                        StationName = c.StationName,
                        Latitude = c.Latitude,
                        Longitude = c.Longitude
                    })
                    .OrderBy(c => c.StationName);
            }

            if (query.NotNullAndEquals("London Terminals"))
            {
                return _londonTerminals;
            }

            return stations
                .Where(c => c.StationName.IndexOf(query, StringComparison.InvariantCultureIgnoreCase) >= 0)
                .Select(c => new CrsStation
                {
                    CrsCode = c.CrsCode,
                    StationName = c.StationName,
                    Latitude = c.Latitude,
                    Longitude = c.Longitude
                })
                .OrderBy(c => c.StationName);
        }

        private async Task<List<CrsStation>> LoadFromCsvAsync()
        {
            _logger.LogInformation("Loading station list from CSV download");

            var url = _config["NaptanStationsUrl"];
            if (string.IsNullOrWhiteSpace(url))
                throw new CrsServiceException("Missing configuration value: NaptanStationsUrl");

            try
            {
                using var stream = await _httpClient.GetStreamAsync(new Uri(url)).ConfigureAwait(false);
                using var reader = new StreamReader(stream);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var result = new List<CrsStation>();

                foreach (var rec in csv.GetRecords<NaptaStation>())
                {
                    if (string.IsNullOrWhiteSpace(rec.CrsCode))
                        continue;

                    if (!seen.Add(rec.CrsCode))
                        continue;

                    result.Add(FromNaptaStation(rec));
                }

                return result;
            }
            catch (Exception e) when (e is HttpRequestException || e is SocketException)
            {
                _logger.LogWarning(e, "Failed to load station list from CSV download");
                throw new CrsServiceException(
                    "The CRS service failed to load the station list from the CSV file download.", e);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load station list from CSV download");
                throw new CrsServiceException(
                    "The CRS service failed to load the station list from the CSV file download.", ex);
            }
        }

        private async Task<List<CrsStation>> LoadFromJsonAddendumAsync()
        {
            var url = _config["RailStationsAddendumUrl"];
            if (string.IsNullOrWhiteSpace(url))
                return new List<CrsStation>(); // optional

            try
            {
                using var stream = await _httpClient.GetStreamAsync(new Uri(url)).ConfigureAwait(false);
                var stations = await JsonSerializer.DeserializeAsync<List<CrsStation>>(stream).ConfigureAwait(false);
                return stations ?? new List<CrsStation>();
            }
            catch (Exception e) when (e is HttpRequestException || e is SocketException)
            {
                _logger.LogWarning(e, "Failed to load station list from JSON addendum (optional)");
                return new List<CrsStation>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load station list from JSON addendum (optional)");
                return new List<CrsStation>();
            }
        }

        private static IReadOnlyList<CrsStation> MergeStations(
            List<CrsStation> csvStations,
            List<CrsStation> jsonStations)
        {
            // Prefer CSV as baseline, then add JSON entries that introduce new CRS codes
            var merged = new List<CrsStation>(csvStations);
            var seen = new HashSet<string>(
                csvStations.Where(s => !string.IsNullOrWhiteSpace(s.CrsCode)).Select(s => s.CrsCode),
                StringComparer.OrdinalIgnoreCase);

            foreach (var s in jsonStations)
            {
                if (string.IsNullOrWhiteSpace(s.CrsCode))
                    continue;

                if (seen.Add(s.CrsCode))
                    merged.Add(s);
            }

            return merged;
        }

        private static IReadOnlyList<CrsStation> ComputeLondonTerminals(IReadOnlyList<CrsStation> stations)
        {
            // https://www.nationalrail.co.uk/times_fares/ticket_types/46587.aspx#terminals
            var lTermCrs = new HashSet<string>(new[]
            {
                "BFR","CST","CHX","CTK","EUS","FST","KGX","LST","LBG","MYB","MOG","OLD","PAD","STP","VXH","VIC","WAT","WAE"
            }, StringComparer.OrdinalIgnoreCase);

            return stations
                .Where(c => lTermCrs.Contains(c.CrsCode))
                .Select(c => new CrsStation
                {
                    CrsCode = c.CrsCode,
                    StationName = c.StationName,
                    Latitude = c.Latitude,
                    Longitude = c.Longitude
                })
                .OrderBy(c => c.StationName)
                .ToList();
        }

        private CrsStation FromNaptaStation(NaptaStation naptaStation)
        {
            var v = GetLatLon(
                double.Parse(naptaStation.Easting, CultureInfo.InvariantCulture),
                double.Parse(naptaStation.Northing, CultureInfo.InvariantCulture));

            return new CrsStation
            {
                StationName = naptaStation.StationName
                    .Replace("Rail Station", "", StringComparison.InvariantCulture)
                    .Trim(),
                CrsCode = naptaStation.CrsCode,
                Latitude = v.Latitude,
                Longitude = v.Longitude
            };
        }

        private LatitudeLongitude GetLatLon(double easting, double northing)
        {
            Cartesian cartesian = Convert.ToCartesian(new Airy1830(),
                new BritishNationalGrid(),
                new EastingNorthing(easting, northing));

            Cartesian wgsCartesian = Transform.Osgb36ToEtrs89(cartesian);

            return Convert.ToLatitudeLongitude(new Wgs84(), wgsCartesian);
        }
    }
}
