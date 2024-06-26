using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Huxley2.Models {
    public class JourneyPlannerRequest {
        private string _originCrs = string.Empty;
        private string _destinationCrs = string.Empty;
        private string _avoidCrs = null;

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

        public string? AvoidCrs
        {
            get => _avoidCrs;
            set => _avoidCrs = value;
        }
     
        [FromQuery]
        public bool ArriveBy { get; set; } = true;

        [FromQuery]
        public int EnquiryType { get; set; } = 0;

        [Required]
        public DateTime PlannedTime { get; set; } 
    }
}


