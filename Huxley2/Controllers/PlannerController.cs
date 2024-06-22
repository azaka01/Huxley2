using Microsoft.AspNetCore.Mvc;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Huxley2.Controllers {

        // GET api/planner/HOU/WAT/2023-06-30T22:00:00.000Z?arriveBy=false
        [HttpGet]
        {
            {
                _logger.LogInformation("OJP API response is ", ojpResponse.GeneratedAt);
                clock.Stop();
                // TODO if journeyDetails null then throw execption
                if (ojpResponse == null)
                {
                    _logger.LogError("null journeyDetails returned");
                    throw new InvalidOperationException("Journey details cannot be null");
                }
                return ojpResponse;
            catch (Exception e)
            {
                // TODO log request