using Huxley2.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Huxley2.Interfaces
{
    public interface IStationService
    {
        IEnumerable<CrsStation> GetLondonTerminals();
        IEnumerable<CrsStation> GetStations(string? query);
        Task LoadStations();
    }
}
