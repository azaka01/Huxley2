using System;
using System.ComponentModel.DataAnnotations;

namespace Huxley2.Models
{
    public class JourneyPlannerRequest
    {
        private string _originCrs = string.Empty;
        private string _destinationCrs = string.Empty;

      /*  [Required]
        [MinLength(3)]
        public string OriginCrs {
            get => _originCrs?.ToUpperInvariant()?.Trim() ?? string.Empty;
            set => _originCrs = value;
        }

        [Required]
        [MinLength(3)]
        public string DestinationCrs {
            get => _destinationCrs?.ToUpperInvariant()?.Trim() ?? string.Empty;
            set => _destinationCrs = value;
        }*/

        //public string realtimeEnquiry { get; set; } = string.Empty;

        //public DateTime ArriveByTime { get; set; }
    }
}

