using Huxley2.Models;
using NreOJPService;
using System.Threading.Tasks;

namespace Huxley2.Interfaces
{
    public interface IJourneyPlannerService
    {
        Task<RealtimeJourneyPlanResponse> GetJourneyDetailsAsync(JourneyPlannerRequest request);
    }
}
