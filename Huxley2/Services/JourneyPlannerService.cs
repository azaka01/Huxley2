using Huxley2.Interfaces;
using Microsoft.Extensions.Logging;
using ServiceReference;
using System;
using System.Threading.Tasks;
using System.ServiceModel;


namespace Huxley2.Services
{
    public class JourneyPlannerService : IJourneyPlannerService
    {
        private readonly ILogger<JourneyPlannerService> _logger;
        private readonly IMapperService _mapperService;
        private readonly jpservices _jpClient;

        public JourneyPlannerService(
            ILogger<JourneyPlannerService> logger,
            IMapperService mapperService,
            jpservices jpClient
        )
        {
            _logger = logger;
            _mapperService = mapperService;
            _jpClient = jpClient;
        }

        async Task<ReturnResponseType> IJourneyPlannerService.GetJourneyDetailsAsync()
        {
            _logger.LogInformation($"Calling GetJourneyDetailsAsync SOAP endpoint");

            try {
                var response = await _jpClient.RealtimeJourneyPlanAsync(
                        _mapperService.MapGetJourneyPlannerRequest()
                        );
              //  _logger.LogInformation($"GetJourneyDetailsAsync response type is " + response.RealtimeJourneyPlanResponse.response);


                if (response.RealtimeJourneyPlanResponse == null) {
                    _logger.LogInformation($"GetJourneyDetailsAsync response is NULL");

                } else {
                    _logger.LogInformation($"GetJourneyDetailsAsync response is " + response.RealtimeJourneyPlanResponse.ToString());
                }

                return response.RealtimeJourneyPlanResponse;
            }
            catch (FaultException<ServiceReference.ReturnResponseType> exception)
            {
                _logger.LogInformation($"GetJourneyDetailsAsync response exception " + exception);
            }
            return null;
        }
    }
}
