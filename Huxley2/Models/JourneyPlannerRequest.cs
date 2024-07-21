using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

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
     
        [FromQuery]
        public int ItemChoiceType { get; set; } = 0;

        [FromQuery]
        public int EnquiryType { get; set; } = 0;

        [FromQuery]
        public string? AvoidCrs { get; set; } = null;

        [FromQuery]
        public string? ViaCrs { get; set; } = null;

        [FromQuery]
        public bool DirectTrains { get; set; } = false;

        [Required]
        public DateTime PlannedTime { get; set; } 
    }
}


