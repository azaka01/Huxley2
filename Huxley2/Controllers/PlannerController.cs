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
            _logger.LogInformation(
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

                _logger.LogInformation("JourneyPlan success. GeneratedAt={GeneratedAt}", ojpResponse.GeneratedAt);
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
                _logger.LogInformation("JourneyPlan elapsed {ElapsedMs}ms", clock.ElapsedMilliseconds);
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
            _logger.LogInformation(
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

                _logger.LogInformation("CallingPoints success. GeneratedAt={GeneratedAt}", ojpResponse.GeneratedAt);
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
                _logger.LogInformation("CallingPoints elapsed {ElapsedMs}ms", clock.ElapsedMilliseconds);
            }
        }

        private static (int StatusCode, string UserMessage) MapOjpFault(string? faultCode, bool isCallingPoints)
        {
            return faultCode switch
            {
                // Applies to either operation (you’ve already seen it on CallingPoints)
                "OutwardDateTimeInThePast" => (
                    StatusCodes.Status400BadRequest,
                    "The time you selected is in the past. Please choose a future time and try again."
                ),

                // JourneyPlan faults
                "ReturnDateTimeBeforeOutwardDate" => (
                    StatusCodes.Status400BadRequest,
                    "Your return date/time must be after your outward date/time. Please update your selection and try again."
                ),

                // CallingPoints faults
                "MatchingJourneyNotFound" when isCallingPoints => (
                    StatusCodes.Status404NotFound,
                    "We couldn’t find a matching journey for the selected route and times. Please check your details and try again."
                ),

                // JourneyPlan: treat as "no services"
                "JourneyPlanBadSearchStatus" => (
                    StatusCodes.Status404NotFound,
                    "No services were found for the journey you requested. Please check your dates, times, or route and try again."
                ),

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
