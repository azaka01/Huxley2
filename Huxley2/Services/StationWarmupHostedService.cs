using Huxley2.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Huxley2.Services
{
    public sealed class StationWarmupHostedService : IHostedService
    {
        private readonly ILogger<StationWarmupHostedService> _logger;
        private readonly IStationService _stationService;

        public StationWarmupHostedService(
            ILogger<StationWarmupHostedService> logger,
            IStationService stationService)
        {
            _logger = logger;
            _stationService = stationService;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Station warmup started");

                await _stationService.LoadStations().ConfigureAwait(false);

                _logger.LogInformation("Station warmup completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Station warmup failed; stations will return 503");
                // Do NOT rethrow unless you want the host to crash
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
