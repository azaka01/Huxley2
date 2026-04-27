using Huxley2.Models;
using System.Threading.Tasks;

namespace Huxley2.Interfaces
{
    public interface IPostcodeJourneyPlannerService
    {
        Task<PostcodeJourneyPlanResponseModel> GetPostcodeJourneyPlanAsync(
            PostcodeJourneyPlannerRequest request,
            string postcode,
            string stationCrs,
            bool originIsPostcode);
    }
}
