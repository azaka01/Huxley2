
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

namespace Huxley2.Services
{
    public class CrsStationService : IStationService
    {
        private readonly ILogger<CrsStationService> _logger;
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;

        private List<CrsStation> _stations = new List<CrsStation>();
        private List<CrsStation> _londonTerminals = new List<CrsStation>();

        public CrsStationService(
                ILogger<CrsStationService> logger,
                IConfiguration config,
                HttpClient httpClient
            )
        {
            _logger = logger;
            _config = config;
            _httpClient = httpClient;
        }

        IEnumerable<CrsStation> IStationService.GetLondonTerminals() {
            throw new System.NotImplementedException();
        }

        public async Task LoadStations() {
            try {
                await GetCrsCodesFromCsvDownload();
                await GetStationsFromJSONSource();
            } catch (CrsServiceException) {
                // A failure here means we can't proceed
                return;
            }
        }

        private async Task GetStationsFromJSONSource() {
            try {
                var jsonUri = new Uri(_config["RailStationsAddendumUrl"]);
                var jsonStream = await _httpClient.GetStreamAsync(jsonUri);
                var stations = await JsonSerializer.DeserializeAsync<List<CrsStation>>(jsonStream);
                _stations.AddRange(stations);

            } catch (Exception e) when (
                  e is HttpRequestException ||
                  e is SocketException
                  ) {
                _logger.LogWarning(e, "Failed to load station list from JSON download");
            } catch (Exception ex) {
                _logger.LogError(ex, "Failed to load station list from JSON download");
                throw new CrsServiceException(
                    "The CRS service failed to load the station list from the JSON file download.", ex);
            }
        }

        public IEnumerable<CrsStation> GetStations(string? query) {
            if (string.IsNullOrWhiteSpace(query)) {
                return _stations.Select(c => new CrsStation { CrsCode = c.CrsCode, StationName = c.StationName, Latitude = c.Latitude, Longitude = c.Longitude })
                    .OrderBy(c => c.StationName);
            }

            if (query.NotNullAndEquals("London Terminals")) {
                return _londonTerminals;
            }

            return _stations.Where(c => c.StationName.IndexOf(query, StringComparison.InvariantCultureIgnoreCase) >= 0)
                .Select(c => new CrsStation { CrsCode = c.CrsCode, StationName = c.StationName, Latitude = c.Latitude, Longitude = c.Longitude })
                .OrderBy(c => c.StationName);
        }

        private async Task GetCrsCodesFromCsvDownload() {
            _logger.LogInformation("Loading station list from CSV download");
            try {
              
                var csvUri = new Uri(_config["NaptanStationsUrl"]);
                var stream = await _httpClient.GetStreamAsync(csvUri);
                using var reader = new StreamReader(stream);
                using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

                using (var csvReader = new CsvReader(new StreamReader(stream), CultureInfo.InvariantCulture)) {
                    _stations.AddRange(csvReader.GetRecords<NaptaStation>().Where(c => _stations.All(code => code.CrsCode != c.CrsCode))
                                    .Select(c => fromNaptaStation(c)));
                }

                // Set London Terminals from codes
                // https://www.nationalrail.co.uk/times_fares/ticket_types/46587.aspx#terminals
                var lTermCrs = new[] { "BFR", "CST", "CHX", "CTK", "EUS", "FST", "KGX", "LST",
                "LBG", "MYB", "MOG", "OLD", "PAD", "STP", "VXH", "VIC", "WAT", "WAE", };
                _londonTerminals.AddRange(_stations.Where(c => lTermCrs.Contains(c.CrsCode))
                    .Select(c => new CrsStation { CrsCode = c.CrsCode, StationName = c.StationName, Latitude = c.Latitude, Longitude = c.Longitude })
                    .OrderBy(c => c.StationName));

            } catch (Exception e) when (
                  e is HttpRequestException ||
                  e is SocketException
                  ) {
                _logger.LogWarning(e, "Failed to load station list from CSV download");
            } catch (Exception ex) {
                _logger.LogError(ex, "Failed to load station list from CSV download");
                throw new CrsServiceException(
                    "The CRS service failed to load the station list from the CSV file download.", ex);
            }
        }

        private CrsStation fromNaptaStation(NaptaStation naptaStation) {
            var v = GetLatLon(double.Parse(naptaStation.Easting, CultureInfo.InvariantCulture), double.Parse(naptaStation.Northing, CultureInfo.InvariantCulture));
            return new CrsStation {
                // NaPTAN suffixes most station names with "Rail Station" which we don't want
                StationName = naptaStation.StationName.Replace("Rail Station", "", StringComparison.InvariantCulture).Trim(),
                CrsCode = naptaStation.CrsCode,
                Latitude = v.Latitude,
                Longitude = v.Longitude
            };
        }

        private LatitudeLongitude GetLatLon(double easting, double northing) {
            var cartesian = GeoUK.Convert.ToCartesian(new Airy1830(),
                                                      new BritishNationalGrid(),
                                                      new EastingNorthing(
                                                            easting,
                                                            northing));
            var wgsCartesian = Transform.Osgb36ToEtrs89(cartesian);
            return GeoUK.Convert.ToLatitudeLongitude(new Wgs84(), wgsCartesian);
        }
    }

}
