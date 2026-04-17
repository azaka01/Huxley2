using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Huxley2.Interfaces;
using Huxley2.Models;
using Huxley2.Security;
using Huxley2.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpenLDBWS;

namespace Huxley2.Controllers
{
    [Route("api/nearby")]
    [ApiController]
    [RequireApiKey]
    public class NearbyController : ControllerBase
    {
        private readonly INearbyStationService _nearbyStationService;
        private readonly IStationBoardService _stationBoardService;
        private readonly IStationService _stationService;
        private readonly ILogger<NearbyController> _logger;
        private readonly IDateTimeService _dateTimeService;

        public NearbyController(
            INearbyStationService nearbyStationService,
            IStationBoardService stationBoardService,
            IStationService stationService,
            ILogger<NearbyController> logger,
            IDateTimeService dateTimeService)
        {
            _nearbyStationService = nearbyStationService;
            _stationBoardService = stationBoardService;
            _stationService = stationService;
            _logger = logger;
            _dateTimeService = dateTimeService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(NearbyServicesResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
        [ProducesDefaultResponseType]
        public async Task<ActionResult<NearbyServicesResponse>> Get(
            [FromQuery] string? lat,
            [FromQuery] string? lng,
            [FromQuery] string? destination,
            [FromQuery] string? radius,
            [FromQuery] string? maxStations,
            [FromQuery] string? numRows,
            [FromQuery] bool expand = false)
        {
            // Validate lat
            if (string.IsNullOrWhiteSpace(lat) || !double.TryParse(lat, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var latValue))
            {
                return BadRequest(new ApiError
                {
                    Code = "INVALID_PARAMETER",
                    Message = "The 'lat' parameter is required and must be a valid numeric value.",
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            if (latValue < -90 || latValue > 90)
            {
                return BadRequest(new ApiError
                {
                    Code = "INVALID_PARAMETER",
                    Message = "The 'lat' parameter must be between -90 and 90.",
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            // Validate lng
            if (string.IsNullOrWhiteSpace(lng) || !double.TryParse(lng, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var lngValue))
            {
                return BadRequest(new ApiError
                {
                    Code = "INVALID_PARAMETER",
                    Message = "The 'lng' parameter is required and must be a valid numeric value.",
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            if (lngValue < -180 || lngValue > 180)
            {
                return BadRequest(new ApiError
                {
                    Code = "INVALID_PARAMETER",
                    Message = "The 'lng' parameter must be between -180 and 180.",
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            // Validate optional destination
            string? destinationCrs = null;
            if (!string.IsNullOrWhiteSpace(destination))
            {
                destinationCrs = destination.Trim().ToUpperInvariant();
                if (!CrsValidator.IsValidCrsCode(destinationCrs))
                {
                    return BadRequest(new ApiError
                    {
                        Code = "INVALID_PARAMETER",
                        Message = "The 'destination' parameter must be a valid 3-letter CRS code.",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                if (_stationService.GetStationByCrsCode(destinationCrs) == null)
                {
                    return BadRequest(new ApiError
                    {
                        Code = "INVALID_PARAMETER",
                        Message = $"The destination station '{destinationCrs}' was not found.",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }
            }

            // Validate optional radius
            double radiusValue = 3;
            if (!string.IsNullOrWhiteSpace(radius))
            {
                if (!double.TryParse(radius, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out radiusValue) || radiusValue <= 0 || radiusValue > 50)
                {
                    return BadRequest(new ApiError
                    {
                        Code = "INVALID_PARAMETER",
                        Message = "The 'radius' parameter must be a positive number up to 50.",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }
            }

            // Validate optional maxStations
            int maxStationsValue = 5;
            if (!string.IsNullOrWhiteSpace(maxStations))
            {
                if (!int.TryParse(maxStations, out maxStationsValue) || maxStationsValue <= 0 || maxStationsValue > 20)
                {
                    return BadRequest(new ApiError
                    {
                        Code = "INVALID_PARAMETER",
                        Message = "The 'maxStations' parameter must be a positive integer up to 20.",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }
            }

            // Validate optional numRows
            int numRowsValue = 4;
            if (!string.IsNullOrWhiteSpace(numRows))
            {
                if (!int.TryParse(numRows, out numRowsValue) || numRowsValue <= 0 || numRowsValue > 150)
                {
                    return BadRequest(new ApiError
                    {
                        Code = "INVALID_PARAMETER",
                        Message = "The 'numRows' parameter must be a positive integer up to 150.",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }
            }

            _logger.LogInformation(
                "Nearby request. Lat={Lat} Lng={Lng} Destination={Destination} Radius={Radius} MaxStations={MaxStations} NumRows={NumRows}",
                latValue, lngValue, destinationCrs, radiusValue, maxStationsValue, numRowsValue);

            // Find nearby stations
            var nearbyStations = _nearbyStationService.FindNearbyStations(latValue, lngValue, radiusValue, maxStationsValue);

            // Fetch services for each station
            var stationResponses = new List<NearbyStationResponse>();

            foreach (var station in nearbyStations)
            {
                try
                {
                    var request = new StationBoardRequest
                    {
                        Crs = station.CrsCode,
                        NumRows = (ushort)numRowsValue,
                        Expand = expand || destinationCrs != null // expand for calling points when destination filtering or explicitly requested
                    };

                    var board = await _stationBoardService.GetDepartureBoardAsync(request);

                    object[] services;

                    if (destinationCrs != null && board is StationBoardWithDetails boardWithDetails)
                    {
                        // Filter services by destination calling points
                        var matchingServices = (boardWithDetails.trainServices ?? Array.Empty<ServiceItemWithCallingPoints>())
                            .Where(s => HasDestinationInCallingPoints(s, destinationCrs))
                            .Cast<object>()
                            .ToArray();

                        services = matchingServices;
                    }
                    else if (destinationCrs != null)
                    {
                        // Destination was requested but board doesn't have calling points — return empty
                        services = Array.Empty<object>();
                    }
                    else if (board is StationBoardWithDetails expandedBoard)
                    {
                        // expand=true without destination filter — return all services with calling points
                        services = expandedBoard.trainServices ?? Array.Empty<object>();
                    }
                    else if (board is StationBoard stationBoard)
                    {
                        services = stationBoard.trainServices ?? Array.Empty<object>();
                    }
                    else
                    {
                        services = Array.Empty<object>();
                    }

                    stationResponses.Add(new NearbyStationResponse
                    {
                        StationName = station.StationName,
                        CrsCode = station.CrsCode,
                        Latitude = station.Latitude,
                        Longitude = station.Longitude,
                        DistanceMiles = station.DistanceMiles,
                        Services = services
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch services for station {CrsCode}", station.CrsCode);

                    // Include station with empty services on failure
                    stationResponses.Add(new NearbyStationResponse
                    {
                        StationName = station.StationName,
                        CrsCode = station.CrsCode,
                        Latitude = station.Latitude,
                        Longitude = station.Longitude,
                        DistanceMiles = station.DistanceMiles,
                        Services = Array.Empty<object>()
                    });
                }
            }

            var response = new NearbyServicesResponse
            {
                GeneratedAt = _dateTimeService.UtcNow.ToString("O"),
                Stations = stationResponses
            };

            return Ok(response);
        }

        private static bool HasDestinationInCallingPoints(ServiceItemWithCallingPoints service, string destinationCrs)
        {
            // Check the service's destination locations
            if (service.destination != null)
            {
                if (service.destination.Any(d => string.Equals(d.crs, destinationCrs, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            // Check subsequent calling points for direct services
            if (service.subsequentCallingPoints != null)
            {
                foreach (var callingPointList in service.subsequentCallingPoints)
                {
                    if (callingPointList.callingPoint != null)
                    {
                        if (callingPointList.callingPoint.Any(cp =>
                            string.Equals(cp.crs, destinationCrs, StringComparison.OrdinalIgnoreCase)))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
