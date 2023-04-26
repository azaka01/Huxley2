using System;
using System.Threading.Tasks;
using Huxley2.Models;
using JPServices;

namespace Huxley2.Interfaces
{
    public interface IJourneyPlannerService
    {
        Task<RealtimeJourneyPlanResponse> GetJourneyDetailsAsync(JourneyPlannerRequest request);
    }
}

