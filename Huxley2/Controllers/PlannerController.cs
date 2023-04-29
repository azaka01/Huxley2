using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Huxley2.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlannerController : ControllerBase
    {
        private readonly ILogger<PlannerController> _logger;


        public PlannerController(
            ILogger<PlannerController> logger)
        {
            _logger = logger;
        }


        // GET api/<PlannerController>/5
        [HttpGet("{id}")]
        public string Get(int id)
        {
            _logger.LogInformation($"Getting planner for query: {id}");
            return "value " + id;
        }
    }
}
