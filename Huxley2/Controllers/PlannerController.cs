using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Huxley2.Interfaces;
using Huxley2.Models;
using Huxley2.Services;
using JPServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Huxley2.Controllers
{
    [ApiController]
    [Route("journey/[controller]")]
    public class PlannerController : ControllerBase {

        private readonly ILogger<PlannerController> _logger;
        private readonly IJourneyPlannerService _journeyPlannerService;


        public PlannerController(
            ILogger<PlannerController> logger,
            IJourneyPlannerService journeyPlannerService
            ) {
            _logger = logger;
            _journeyPlannerService = journeyPlannerService;
        }

        [HttpGet("getRoute")]
        [ProducesResponseType(typeof(JPServices.RealtimeJourneyPlanResponse), StatusCodes.Status200OK)]
        [ProducesDefaultResponseType]
        public async Task<object> Get([FromRoute] JourneyPlannerRequest request, [FromQuery] JourneyPlannerRequest queryRequest) {
            try {
                var clock = Stopwatch.StartNew();
                var journeyDetails = await _journeyPlannerService.GetJourneyDetailsAsync(request);
                clock.Stop();
                _logger.LogInformation("OJP API time {ElapsedMilliseconds:#,#}ms",
                    clock.ElapsedMilliseconds);
                return journeyDetails;
            } catch (Exception e) {
                _logger.LogError(e, "JourneyPlanner request call failed");
                throw;
            }
        }
    }
}

