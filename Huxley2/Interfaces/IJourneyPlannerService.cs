using Huxley2.Models;
using System.Threading.Tasks;

namespace Huxley2.Interfaces
{
    public interface IJourneyPlannerService
    {
        Task<OjpResponse> GetJourneyDetailsAsync(JourneyPlannerRequest request);
        Task<OjpCallingPointsResponse> GetJourneyCallingPointsAsync(JourneyCallingPointsRequest request);
    }
}
