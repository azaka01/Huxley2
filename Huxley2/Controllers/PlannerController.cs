using Microsoft.AspNetCore.Mvc;using Microsoft.Extensions.Logging;using System.Diagnostics;using System;using Huxley2.Interfaces;using System.Threading.Tasks;using NreOJPService;using Microsoft.AspNetCore.Http;using Huxley2.Models;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Huxley2.Controllers {    [Route("api/[controller]")]    [ApiController]    public class PlannerController : ControllerBase {        private readonly ILogger<PlannerController> _logger;        private readonly IJourneyPlannerService _journeyPlannerService;        public PlannerController(            ILogger<PlannerController> logger,            IJourneyPlannerService journeyPlannerService) {            _logger = logger;            _journeyPlannerService = journeyPlannerService;        }

        // GET api/planner/HOU/WAT/2023-06-30T22:00:00.000Z?arriveBy=false&enquiryType=0
        [HttpGet]        [Route("{originCrs}/{destinationCrs}/{plannedTime}")]
        [Route("{originCrs}/{destinationCrs}/{avoidCrs}/{plannedTime}")]        [ProducesResponseType(typeof(ReturnResponseType), StatusCodes.Status200OK)]        [ProducesResponseType(typeof(OjpResponse), StatusCodes.Status200OK)]        [ProducesDefaultResponseType]        public async Task<OjpResponse> Get([FromRoute] JourneyPlannerRequest request)
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
                return ojpResponse;            }
            catch (Exception e)
            {                _logger.LogError(e, "JourneyPlanner request call failed");
                // TODO log request                throw;            }        }

        // GET api/planner/points/ABW/PAD/2024-06-25T05:34/2024-06-25T06:03
        [HttpGet]        [Route("points/{originCrs}/{destinationCrs}/{departureTime}/{arrivalTime}")]        [ProducesResponseType(typeof(ReturnResponseType), StatusCodes.Status200OK)]        [ProducesResponseType(typeof(OjpResponse), StatusCodes.Status200OK)]        [ProducesDefaultResponseType]        public async Task<OjpCallingPointsResponse> GetPoints([FromRoute] JourneyCallingPointsRequest request)
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
                return ojpResponse;            }
            catch (Exception e)
            {                _logger.LogError(e, "JourneyPlanner request call failed");
                // TODO log request
                throw;            }        }    }}