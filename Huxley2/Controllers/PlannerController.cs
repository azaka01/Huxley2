using Microsoft.AspNetCore.Mvc;using Microsoft.Extensions.Logging;using System.Diagnostics;using System;using Huxley2.Interfaces;using System.Threading.Tasks;using NreOJPService;using Microsoft.AspNetCore.Http;using Huxley2.Models;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Huxley2.Controllers
{    [Route("api/[controller]")]    [ApiController]    public class PlannerController : ControllerBase
    {        private readonly ILogger<PlannerController> _logger;        private readonly IJourneyPlannerService _journeyPlannerService;        public PlannerController(            ILogger<PlannerController> logger,            IJourneyPlannerService journeyPlannerService)
        {            _logger = logger;            _journeyPlannerService = journeyPlannerService;        }

        // GET api/planner/HOU/WAT/2023-06-30T22:00:00.000Z?itemChoiceType=1&enquiryType=0
        // GET api/planner/HOU/WAT/2023-06-30T22:00:00?itemChoiceType=1&enquiryType=0&via=FEL&avoid=FEL&directTrains=true
        [HttpGet]        [Route("{originCrs}/{destinationCrs}/{plannedTime}")]        [ProducesResponseType(typeof(OjpResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status502BadGateway)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status504GatewayTimeout)]        [ProducesDefaultResponseType]        public async Task<ActionResult<OjpResponse>> Get([FromRoute] JourneyPlannerRequest request)
        {            _logger.LogInformation($"Getting planner for query: {request.OriginCrs}");            try
            {                var clock = Stopwatch.StartNew();                var ojpResponse = await _journeyPlannerService.GetJourneyDetailsAsync(request);
                _logger.LogInformation("OJP API response is ", ojpResponse.GeneratedAt);
                clock.Stop();                _logger.LogInformation("OJP API time {ElapsedMilliseconds:#,#}ms",                    clock.ElapsedMilliseconds);
                // TODO if journeyDetails null then throw execption
                if (ojpResponse == null)
                {
                    _logger.LogError("null journeyDetails returned");
                    throw new InvalidOperationException("Journey details cannot be null");
                }
                return Ok(ojpResponse);            }
            catch (Huxley2.Exceptions.OjpFaultException ex)
            {
                var (status, message) = MapOjpFault(ex.FaultCode, isCallingPoints: false);

                _logger.LogWarning(ex,
                    "OJP SOAP fault. Status={Status} Code={Code} Details={Details}",
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
                _logger.LogWarning(ex, "OJP SOAP timeout");

                return StatusCode(StatusCodes.Status504GatewayTimeout, new ApiError
                {
                    Code = "OJP_TIMEOUT",
                    Message = "Journey planner is taking too long to respond. Please try again.",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (Huxley2.Exceptions.OjpUpstreamException ex)
            {
                _logger.LogError(ex, "OJP upstream failure");

                return StatusCode(StatusCodes.Status502BadGateway, new ApiError
                {
                    Code = "OJP_UPSTREAM_ERROR",
                    Message = "Journey planner is temporarily unavailable. Please try again.",
                    TraceId = HttpContext.TraceIdentifier
                });
            }            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled planner exception");
                throw;

            }        }

        // GET api/planner/points/ABW/PAD/2024-06-25T05:34/2024-06-25T06:03
        [HttpGet]        [Route("points/{originCrs}/{destinationCrs}/{departureTime}/{arrivalTime}")]        [ProducesResponseType(typeof(OjpResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status502BadGateway)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status504GatewayTimeout)]        [ProducesDefaultResponseType]        public async Task<ActionResult<OjpCallingPointsResponse>> GetPoints([FromRoute] JourneyCallingPointsRequest request)
        {            _logger.LogInformation($"Getting planner for query: {request.OriginCrs}");            try
            {                var clock = Stopwatch.StartNew();                var ojpResponse = await _journeyPlannerService.GetJourneyCallingPointsAsync(request);
                _logger.LogInformation("OJP API response is ", ojpResponse.GeneratedAt);
                clock.Stop();                _logger.LogInformation("OJP API time {ElapsedMilliseconds:#,#}ms",                    clock.ElapsedMilliseconds);
                // TODO if journeyDetails null then throw execption
                if (ojpResponse == null)
                {
                    _logger.LogError("null journeyDetails returned");
                    throw new InvalidOperationException("Journey details cannot be null");
                }
                return Ok(ojpResponse);            }
            catch (Huxley2.Exceptions.OjpFaultException ex)
            {
                var (status, message) = MapOjpFault(ex.FaultCode, isCallingPoints: true);

                _logger.LogWarning(ex,
                    "OJP SOAP fault (calling points). Status={Status} Code={Code} Details={Details}",
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
                _logger.LogWarning(ex, "OJP SOAP timeout (calling points)");

                return StatusCode(StatusCodes.Status504GatewayTimeout, new ApiError
                {
                    Code = "OJP_TIMEOUT",
                    Message = "Calling points are taking too long to respond. Please try again.",
                    TraceId = HttpContext.TraceIdentifier
                });
            }

            catch (Huxley2.Exceptions.OjpUpstreamException ex)
            {
                _logger.LogError(ex, "OJP upstream failure (calling points)");

                return StatusCode(StatusCodes.Status502BadGateway, new ApiError
                {
                    Code = "OJP_UPSTREAM_ERROR",
                    Message = "Calling points are temporarily unavailable. Please try again.",
                    TraceId = HttpContext.TraceIdentifier
                });
            }            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled planner exception");
                throw;

            }        }

        private static (int StatusCode, string UserMessage) MapOjpFault(string? faultCode, bool isCallingPoints)
        {
            return faultCode switch
            {
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

                // JourneyPlan: treat as "no services" (e.g., Xmas Day / no timetable)
                "JourneyPlanBadSearchStatus" => (
                    StatusCodes.Status404NotFound,
                    "No services were found for the journey you requested. Please check your dates, times, or route and try again."
                ),

                // Default
                _ => (
                    StatusCodes.Status502BadGateway,
                    isCallingPoints
                        ? "Calling points service returned an error. Please try again."
                        : "Journey planner returned an error. Please try again."
                )
            };
        }
    }}