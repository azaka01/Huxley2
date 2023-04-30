using ServiceReference;
using System.Threading.Tasks;

namespace Huxley2.Interfaces
{
    public interface IJourneyPlannerService
    {
        Task<ReturnResponseType> GetJourneyDetailsAsync();
    }
}
