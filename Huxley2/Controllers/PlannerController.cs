using Microsoft.AspNetCore.Mvc;using Microsoft.Extensions.Logging;using System.Diagnostics;using System;using Huxley2.Interfaces;using System.Threading.Tasks;using NreOJPService;using Microsoft.AspNetCore.Http;using Huxley2.Models;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Huxley2.Controllers {    [Route("api/[controller]")]    [ApiController]    public class PlannerController : ControllerBase {        private readonly ILogger<PlannerController> _logger;        private readonly IJourneyPlannerService _journeyPlannerService;        public PlannerController(            ILogger<PlannerController> logger,            IJourneyPlannerService journeyPlannerService) {            _logger = logger;            _journeyPlannerService = journeyPlannerService;        }


        // GET api/planner/HOU/WAT/2023-06-30T22:00:00.000Z?arriveBy=false
        [HttpGet]        [Route("{originCrs}/{destinationCrs}/{plannedTime}")]        [ProducesResponseType(typeof(ReturnResponseType), StatusCodes.Status200OK)]        [ProducesResponseType(typeof(RealtimeJourneyPlanResponse), StatusCodes.Status200OK)]        [ProducesDefaultResponseType]        public async Task<RealtimeJourneyPlanResponse> Get([FromRoute] JourneyPlannerRequest request) {            _logger.LogInformation($"Getting planner for query: {request.OriginCrs}");            try {                var clock = Stopwatch.StartNew();                var journeyDetails = await _journeyPlannerService.GetJourneyDetailsAsync(request);
                //   _logger.LogInformation("OJP API response is ", journeyDetails.response.ToString());
                clock.Stop();                _logger.LogInformation("OJP API time {ElapsedMilliseconds:#,#}ms",                    clock.ElapsedMilliseconds);
                // TODO if journeyDetails null then throw execption
                return journeyDetails;            } catch (Exception e) {                _logger.LogError(e, "JourneyPlanner request call failed");                throw;            }        }    }}