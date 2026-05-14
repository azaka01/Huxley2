using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Huxley2.Models
{
    public class PostcodeJourneyPlannerRequest
    {
        [Required]
        [FromQuery]
        public string Origin { get; set; } = string.Empty;

        [Required]
        [FromQuery]
        public string Destination { get; set; } = string.Empty;

        [Required]
        [FromQuery]
        public DateTime PlannedTime { get; set; }

        [FromQuery]
        public bool DirectTrains { get; set; } = false;

        [FromQuery]
        public int ItemChoiceType { get; set; } = 0;

        [FromQuery]
        public int EnquiryType { get; set; } = 0;
    }
}
