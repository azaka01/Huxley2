using System;
using Huxley2.Interfaces;
using Microsoft.Extensions.Logging;
using JPServices;
using System.Net.Mail;
using System.Threading.Tasks;
using Huxley2.Models;

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

        async Task<RealtimeJourneyPlanResponse> IJourneyPlannerService.GetJourneyDetailsAsync(JourneyPlannerRequest request) {
            _logger.LogInformation($"Calling GetJourneyDetailsAsync SOAP endpoint");

            var response = await _jpClient.RealtimeJourneyPlanAsync(
                    _mapperService.MapGetJourneyPlannerRequest(request)
                    );

            return response.RealtimeJourneyPlanResponse;
        }
    }
}

