// Written by Awais Zaka EUPL-1.2 (see the LICENSE file for the full license governing this code).

using System.Collections.Generic;
using Huxley2.Interfaces;
using Huxley2.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Huxley2.Controllers {
    [ApiController]
    [Route("[controller]")]
    public class StationsController : ControllerBase {
        private readonly ILogger<StationsController> _logger;
        private readonly IStationService _stationService;

        public StationsController(
            ILogger<StationsController> logger,
            IStationService stationService
            ) {
            _logger = logger;
            _stationService = stationService;
        }

        [HttpGet]
        [Route("")]
        [Route("{query}")]
        [ProducesResponseType(typeof(IEnumerable<CrsStation>), StatusCodes.Status200OK)]
        [ProducesDefaultResponseType]
        public IEnumerable<CrsStation> Get([FromRoute] string? query) {
            _logger.LogInformation($"Getting stations for query: {query}");
            return _stationService.GetStations(query);
        }
    }
}
