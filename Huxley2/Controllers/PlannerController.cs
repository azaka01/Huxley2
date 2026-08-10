using Huxley2.Interfaces;
using Huxley2.Models;
using Huxley2.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.ServiceModel; 
using System.Threading.Tasks;

namespace Huxley2.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [RequireApiKey]
    public class PlannerController : ControllerBase
    {
        private readonly ILogger<PlannerController> _logger;
        private readonly IJourneyPlannerService _journeyPlannerService;

        public PlannerController(
            ILogger<PlannerController> logger,
            IJourneyPlannerService journeyPlannerService)
        {
            _logger = logger;
            _journeyPlannerService = journeyPlannerService;
        }

        // GET api/planner/HOU/WAT/2023-06-30T22:00:00.000Z?itemChoiceType=1&enquiryType=0 
        // GET api/planner/HOU/WAT/2023-06-30T22:00:00?itemChoiceType=1&enquiryType=0&via=FEL&avoid=FEL&directTrains=true 
        [HttpGet]
        [Route("{originCrs}/{destinationCrs}/{plannedTime}")]
        [ProducesResponseType(typeof(OjpResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status502BadGateway)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status504GatewayTimeout)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<OjpResponse>> Get([FromRoute] JourneyPlannerRequest request)
        {
            _logger.LogDebug(
                "JourneyPlan request. Origin={Origin} Destination={Destination} PlannedTime={PlannedTime}",
                request.OriginCrs, request.DestinationCrs, request.PlannedTime);

            var clock = Stopwatch.StartNew();

            try
            {
                var ojpResponse = await _journeyPlannerService.GetJourneyDetailsAsync(request);

                if (ojpResponse == null)
                {
                    _logger.LogError("JourneyPlan returned null response (unexpected).");
                    return StatusCode(StatusCodes.Status502BadGateway, new ApiError
                    {
                        Code = "OJP_EMPTY_RESPONSE",
                        Message = "Journey planner returned an empty response. Please try again.",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                _logger.LogDebug("JourneyPlan success. GeneratedAt={GeneratedAt}", ojpResponse.GeneratedAt);
                return Ok(ojpResponse);
            }
            catch (Huxley2.Exceptions.OjpFaultException ex)
            {
                var (status, message) = MapOjpFault(ex.FaultCode, isCallingPoints: false);

                _logger.LogWarning(ex,
                    "JourneyPlan fault. Status={Status} Code={Code} Details={Details}",
                    status, ex.FaultCode, ex.FaultDetails);

                return StatusCode(status, new ApiError
                {
                    Code = ex.FaultCode ?? "OJP_FAULT",
                    Message = message,
                    Details = ex.FaultDetails,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(ex, "JourneyPlan timeout");
                return StatusCode(StatusCodes.Status504GatewayTimeout, new ApiError
                {
                    Code = "OJP_TIMEOUT",
                    Message = "Journey planner is taking too long to respond. Please try again.",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (CommunicationException ex)
            {
                _logger.LogError(ex, "JourneyPlan upstream communication failure");
                return StatusCode(StatusCodes.Status502BadGateway, new ApiError
                {
                    Code = "OJP_UPSTREAM_COMMUNICATION",
                    Message = "Journey planner is temporarily unavailable. Please try again.",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (Huxley2.Exceptions.OjpUpstreamException ex)
            {
                _logger.LogError(ex, "JourneyPlan upstream failure");
                return StatusCode(StatusCodes.Status502BadGateway, new ApiError
                {
                    Code = "OJP_UPSTREAM_ERROR",
                    Message = "Journey planner is temporarily unavailable. Please try again.",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Invalid operation in JourneyPlan");
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiError
                {
                    Code = "INVALID_OPERATION",
                    Message = "An unexpected error occurred. Please try again.",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning(ex, "JourneyPlan request cancelled");
                throw;
            }
            finally
            {
                clock.Stop();
                _logger.LogDebug("JourneyPlan elapsed {ElapsedMs}ms", clock.ElapsedMilliseconds);
            }
        }


        // GET api/planner/points/ABW/PAD/2024-06-25T05:34/2024-06-25T06:03 
        [HttpGet]
        [Route("points/{originCrs}/{destinationCrs}/{departureTime}/{arrivalTime}")]
        [ProducesResponseType(typeof(OjpCallingPointsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status502BadGateway)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status504GatewayTimeout)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<OjpCallingPointsResponse>> GetPoints([FromRoute] JourneyCallingPointsRequest request)
        {
            _logger.LogDebug(
                "CallingPoints request. Origin={Origin} Destination={Destination} Departure={Departure} Arrival={Arrival}",
                request.OriginCrs, request.DestinationCrs, request.DepartureTime, request.ArrivalTime);

            var clock = Stopwatch.StartNew();

            try
            {
                var ojpResponse = await _journeyPlannerService.GetJourneyCallingPointsAsync(request);

                if (ojpResponse == null)
                {
                    _logger.LogError("CallingPoints returned null response (unexpected).");
                    return StatusCode(StatusCodes.Status502BadGateway, new ApiError
                    {
                        Code = "OJP_EMPTY_RESPONSE",
                        Message = "Calling points returned an empty response. Please try again.",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                _logger.LogDebug("CallingPoints success. GeneratedAt={GeneratedAt}", ojpResponse.GeneratedAt);
                return Ok(ojpResponse);
            }
            catch (Huxley2.Exceptions.OjpFaultException ex)
            {
                var (status, message) = MapOjpFault(ex.FaultCode, isCallingPoints: true);

                _logger.LogWarning(ex,
                    "CallingPoints fault. Status={Status} Code={Code} Details={Details}",
                    status, ex.FaultCode, ex.FaultDetails);

                return StatusCode(status, new ApiError
                {
                    Code = ex.FaultCode ?? "OJP_FAULT",
                    Message = message,
                    Details = ex.FaultDetails,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(ex, "CallingPoints timeout");
                return StatusCode(StatusCodes.Status504GatewayTimeout, new ApiError
                {
                    Code = "OJP_TIMEOUT",
                    Message = "Calling points are taking too long to respond. Please try again.",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (CommunicationException ex)
            {
                _logger.LogError(ex, "CallingPoints upstream communication failure");
                return StatusCode(StatusCodes.Status502BadGateway, new ApiError
                {
                    Code = "OJP_UPSTREAM_COMMUNICATION",
                    Message = "Calling points are temporarily unavailable. Please try again.",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (Huxley2.Exceptions.OjpUpstreamException ex)
            {
                _logger.LogError(ex, "CallingPoints upstream failure");
                return StatusCode(StatusCodes.Status502BadGateway, new ApiError
                {
                    Code = "OJP_UPSTREAM_ERROR",
                    Message = "Calling points are temporarily unavailable. Please try again.",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Invalid operation in CallingPoints");
                return StatusCode(StatusCodes.Status500InternalServerError, new ApiError
                {
                    Code = "INVALID_OPERATION",
                    Message = "An unexpected error occurred. Please try again.",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning(ex, "CallingPoints request cancelled");
                throw;
            }
            finally
            {
                clock.Stop();
                _logger.LogDebug("CallingPoints elapsed {ElapsedMs}ms", clock.ElapsedMilliseconds);
            }
        }

        private static (int StatusCode, string UserMessage) MapOjpFault(string? faultCode, bool isCallingPoints)
        {
            return faultCode switch
            {
                // Date/time validation errors → 400
                "OutwardDateTimeInThePast" => (
                    StatusCodes.Status400BadRequest,
                    "The time you selected is in the past. Please choose a future time and try again."
                ),
                "ReturnDateTimeBeforeOutwardDate" => (
                    StatusCodes.Status400BadRequest,
                    "Your return date/time must be after your outward date/time. Please update your selection and try again."
                ),
                "ReturnDateTimeInThePast" => (
                    StatusCodes.Status400BadRequest,
                    "The return time is in the past. Please choose a future time."
                ),

                // Station validation errors → 400
                "StationDoesNotExist" => (
                    StatusCodes.Status400BadRequest,
                    "The station code provided is not recognised. Please check and try again."
                ),
                "InvalidViaStation" => (StatusCodes.Status400BadRequest, "The ‘via’ station is not a valid National Rail station."),
                "ViaStationSameAsFrom" => (StatusCodes.Status400BadRequest, "The ‘via’ station cannot be the same as the origin station."),
                "ViaStationSameAsTo" => (StatusCodes.Status400BadRequest, "The ‘via’ station cannot be the same as the destination station."),
                "InvalidAvoidStation" => (StatusCodes.Status400BadRequest, "The ‘avoid’ station is not a valid National Rail station."),
                "AvoidStationSameAsFrom" => (StatusCodes.Status400BadRequest, "The ‘avoid’ station cannot be the same as the origin station."),
                "AvoidStationSameAsTo" => (StatusCodes.Status400BadRequest, "The ‘avoid’ station cannot be the same as the destination station."),
                "InvalidExcludeStation" => (StatusCodes.Status400BadRequest, "The ‘exclude’ station is not a valid National Rail station."),
                "ExcludeStationSameAsFrom" => (StatusCodes.Status400BadRequest, "The ‘exclude’ station cannot be the same as the origin station."),
                "ExcludeStationSameAsTo" => (StatusCodes.Status400BadRequest, "The ‘exclude’ station cannot be the same as the destination station."),
                "InvalidInterchangeStation" => (StatusCodes.Status400BadRequest, "The ‘interchange’ station is not a valid National Rail station."),
                "InterchangeStationSameAsFrom" => (StatusCodes.Status400BadRequest, "The ‘interchange’ station cannot be the same as the origin station."),
                "InterchangeStationSameAsTo" => (StatusCodes.Status400BadRequest, "The ‘interchange’ station cannot be the same as the destination station."),
                "InvalidIncludeStation" => (StatusCodes.Status400BadRequest, "The ‘include’ station is not a valid National Rail station."),
                "IncludeStationSameAsFrom" => (StatusCodes.Status400BadRequest, "The ‘include’ station cannot be the same as the origin station."),

                // Postcode errors → 400/404
                "PostcodeMustBeProvided" => (StatusCodes.Status400BadRequest, "A postcode must be provided for either the origin or destination."),
                "PostcodeDoesNotHaveAnyStations" => (StatusCodes.Status404NotFound, "No stations were found near the postcode provided."),
                "PostcodeNotAllowedForFromAndTo" => (StatusCodes.Status400BadRequest, "Postcodes cannot be used for both origin and destination. One must be a station."),
                "InvalidPostcode" => (StatusCodes.Status400BadRequest, "The postcode provided is not valid."),

                // Other validation → 400
                "UnknownRailcardCode" => (StatusCodes.Status400BadRequest, "The railcard code provided is not recognised."),
                "TrainOperatingCompanyNotFound" => (StatusCodes.Status400BadRequest, "The train operator code provided is not recognised."),

                // No results found → 404
                "NoJourneysFound" => (
                    StatusCodes.Status404NotFound,
                    "No journeys were found for your request. Please check your dates, times, or route and try again."
                ),
                "JourneyPlanBadSearchStatus" => (
                    StatusCodes.Status404NotFound,
                    "No services were found for the journey you requested. Please check your dates, times, or route and try again."
                ),
                "MatchingJourneyNotFound" when isCallingPoints => (
                    StatusCodes.Status404NotFound,
                    "We couldn’t find a matching journey for the selected route and times. Please check your details and try again."
                ),

                // Upstream/timeout errors → 502/504
                "JourneyPlanTimeout" or "DepartureBoardTimeout" => (
                    StatusCodes.Status504GatewayTimeout,
                    "The journey planner is taking too long to respond. Please try again."
                ),
                "JourneyPlanError" or "JourneyPlanNrsError" or "JourneyPlanNrsBadStatus" or "OJPOtherError" => (
                    StatusCodes.Status502BadGateway,
                    "The journey planner encountered an error. Please try again."
                ),

                // Default fallback → 502
                _ => (
                    StatusCodes.Status502BadGateway,
                    isCallingPoints
                        ? "Calling points service returned an error. Please try again."
                        : "Journey planner returned an error. Please try again."
                )
            };
        }
    }
}
