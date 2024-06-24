
using System.ComponentModel.DataAnnotations;
using System;

namespace Huxley2.Models
{
    public class JourneyCallingPointsRequest
    {
        private string _originCrs = string.Empty;
        private string _destinationCrs = string.Empty;

        [Required]
        [MinLength(3)]
        public string OriginCrs
        {
            get => _originCrs?.ToUpperInvariant()?.Trim() ?? string.Empty;
            set => _originCrs = value;
        }

        [Required]
        [MinLength(3)]
        public string DestinationCrs
        {
            get => _destinationCrs?.ToUpperInvariant()?.Trim() ?? string.Empty;
            set => _destinationCrs = value;
        }

        [Required]
        public DateTime DepartureTime { get; set; }

        [Required]
        public DateTime ArrivalTime { get; set; }
    }
}
