using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using NreOJPService;

namespace Huxley2.Models {
    public class JourneyPlannerRequest {
        private string _originCrs = string.Empty;
        private string _destinationCrs = string.Empty;

        [Required]
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
        }

        //public string realtimeEnquiry { get; set; } = string.Empty;

        [FromQuery]
        public bool ArriveBy { get; set; } = true;

        [Required]
        public DateTime PlannedTime { get; set; } 
    }
}


