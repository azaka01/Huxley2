using Huxley2.Interfaces;
using Huxley2.Models;
using Huxley2.Security;
using Huxley2.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.ServiceModel;
using System.Threading.Tasks;

namespace Huxley2.Controllers
{
    [Route("api/planner/postcode")]
    [ApiController]
    [RequireApiKey]
    public class PostcodePlannerController : ControllerBase
    {
        private readonly ILogger<PostcodePlannerController> _logger;
        private readonly IPostcodeJourneyPlannerService _postcodeService;

        public PostcodePlannerController(
            ILogger<PostcodePlannerController> logger,
            IPostcodeJourneyPlannerService postcodeService)
        {
            _logger = logger;
            _postcodeService = postcodeService;
        }

        // GET api/planner/postcode?origin=SW1A1AA&destination=PAD&plannedTime=2024-06-30T10:00:00
        [HttpGet]
        [ProducesResponseType(typeof(PostcodeJourneyPlanResponseModel), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status502BadGateway)]
        [ProducesResponseType(typeof(ApiError), StatusCodes.Status504GatewayTimeout)]
        public async Task<ActionResult<PostcodeJourneyPlanResponseModel>> Get(
            [FromQuery] PostcodeJourneyPlannerRequest request)
        {
            _logger.LogInformation(
                "PostcodeJourneyPlan request. Origin={Origin} Destination={Destination} PlannedTime={PlannedTime}",
                request.Origin, request.Destination, request.PlannedTime);

            var clock = Stopwatch.StartNew();

            try
            {
                // Classify origin and destination
                var originUpper = request.Origin?.Trim().ToUpperInvariant() ?? string.Empty;
                var destUpper = request.Destination?.Trim().ToUpperInvariant() ?? string.Empty;

                var originIsCrs = CrsValidator.IsValidCrsCode(originUpper);
                var destIsCrs = CrsValidator.IsValidCrsCode(destUpper);

                // Neither is a postcode (both are CRS) → use standard planner
                if (originIsCrs && destIsCrs)
                {
                    return BadRequest(new ApiError
                    {
                        Code = "USE_STANDARD_PLANNER",
                        Message = "Both origin and destination are station codes. Use the standard journey planner instead.",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                // Both are postcodes → not allowed
                if (!originIsCrs && !destIsCrs)
                {
                    return BadRequest(new ApiError
                    {
                        Code = "POSTCODE_NOT_ALLOWED_FOR_BOTH",
                        Message = "Postcodes cannot be used for both origin and destination. One must be a station code.",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                // Determine which is the postcode and which is the CRS
                bool originIsPostcode = !originIsCrs;
                var rawPostcode = originIsPostcode ? originUpper : destUpper;
                var stationCrs = originIsPostcode ? destUpper : originUpper;

                // Validate postcode format
                if (!PostcodeValidator.IsValidUkPostcode(rawPostcode))
                {
                    return BadRequest(new ApiError
                    {
                        Code = "INVALID_POSTCODE_FORMAT",
                        Message = "The postcode provided is not a valid UK postcode format.",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                var normalisedPostcode = PostcodeValidator.NormalisePostcode(rawPostcode);

                var result = await _postcodeService.GetPostcodeJourneyPlanAsync(
                    request, normalisedPostcode, stationCrs, originIsPostcode);

                if (result == null)
                {
                    _logger.LogError("PostcodeJourneyPlan returned null response (unexpected).");
                    return StatusCode(StatusCodes.Status502BadGateway, new ApiError
                    {
                        Code = "OJP_EMPTY_RESPONSE",
                        Message = "Postcode journey planner returned an empty response. Please try again.",
                        TraceId = HttpContext.TraceIdentifier
                    });
                }

                _logger.LogInformation("PostcodeJourneyPlan success. GeneratedAt={GeneratedAt}", result.GeneratedAt);
                return Ok(result);
            }
            catch (Huxley2.Exceptions.OjpFaultException ex)
            {
                var (status, message) = MapOjpFault(ex.FaultCode);

                _logger.LogWarning(ex,
                    "PostcodeJourneyPlan fault. Status={Status} Code={Code} Details={Details}",
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
                _logger.LogWarning(ex, "PostcodeJourneyPlan timeout");
                return StatusCode(StatusCodes.Status504GatewayTimeout, new ApiError
                {
                    Code = "OJP_TIMEOUT",
                    Message = "Postcode journey planner is taking too long to respond. Please try again.",
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (CommunicationException ex)
            {
                _logger.LogError(ex, "PostcodeJourneyPlan upstream communication failure");
                var innerMessage = ex.InnerException?.Message;
                return StatusCode(StatusCodes.Status502BadGateway, new ApiError
                {
                    Code = "OJP_UPSTREAM_COMMUNICATION",
                    Message = "Unable to connect to the journey planner service. This may be a temporary issue — please try again shortly.",
                    Details = innerMessage,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (Huxley2.Exceptions.OjpUpstreamException ex)
            {
                _logger.LogError(ex, "PostcodeJourneyPlan upstream failure");
                return StatusCode(StatusCodes.Status502BadGateway, new ApiError
                {
                    Code = "OJP_UPSTREAM_ERROR",
                    Message = "The journey planner service returned an unexpected response. Please try again.",
                    Details = ex.Message,
                    TraceId = HttpContext.TraceIdentifier
                });
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning(ex, "PostcodeJourneyPlan request cancelled");
                throw;
            }
            finally
            {
                clock.Stop();
                _logger.LogInformation("PostcodeJourneyPlan elapsed {ElapsedMs}ms", clock.ElapsedMilliseconds);
            }
        }

        private static (int StatusCode, string UserMessage) MapOjpFault(string? faultCode)
        {
            return faultCode switch
            {
                "OutwardDateTimeInThePast" => (
                    StatusCodes.Status400BadRequest,
                    "The time you selected is in the past. Please choose a future time and try again."),
                "StationDoesNotExist" => (
                    StatusCodes.Status400BadRequest,
                    "The station code provided is not recognised. Please check and try again."),
                "PostcodeMustBeProvided" => (
                    StatusCodes.Status400BadRequest,
                    "A postcode must be provided for either the origin or destination."),
                "PostcodeDoesNotHaveAnyStations" => (
                    StatusCodes.Status404NotFound,
                    "No stations were found near the postcode provided."),
                "PostcodeNotAllowedForFromAndTo" => (
                    StatusCodes.Status400BadRequest,
                    "Postcodes cannot be used for both origin and destination. One must be a station."),
                "InvalidPostcode" => (
                    StatusCodes.Status400BadRequest,
                    "The postcode provided is not valid."),
                "NoJourneysFound" => (
                    StatusCodes.Status404NotFound,
                    "No journeys were found for your request. Please check your dates, times, or route and try again."),
                "JourneyPlanTimeout" or "DepartureBoardTimeout" => (
                    StatusCodes.Status504GatewayTimeout,
                    "The journey planner is taking too long to respond. Please try again."),
                "JourneyPlanError" or "JourneyPlanNrsError" or "JourneyPlanNrsBadStatus" or "OJPOtherError" => (
                    StatusCodes.Status502BadGateway,
                    "The journey planner encountered an error. Please try again."),
                _ => (
                    StatusCodes.Status502BadGateway,
                    "Postcode journey planner returned an error. Please try again.")
            };
        }
    }
}
