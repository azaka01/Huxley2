// © James Singleton. EUPL-1.2 (see the LICENSE file for the full license governing this code).

using FakeItEasy;
using Huxley2.Controllers;
using Huxley2.Exceptions;
using Huxley2.Interfaces;
using Huxley2.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.ServiceModel;
using System.Threading.Tasks;
using Xunit;

namespace Huxley2Tests.Controllers
{
    public class PostcodePlannerControllerTests
    {
        private readonly IPostcodeJourneyPlannerService _service;
        private readonly PostcodePlannerController _controller;

        public PostcodePlannerControllerTests()
        {
            _service = A.Fake<IPostcodeJourneyPlannerService>();
            _controller = new PostcodePlannerController(
                A.Fake<ILogger<PostcodePlannerController>>(),
                _service);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        [Fact]
        public async Task Get_WithPostcodeOriginAndCrsDestination_ReturnsOk()
        {
            var request = new PostcodeJourneyPlannerRequest
            {
                Origin = "SW1A 1AA",
                Destination = "PAD",
                PlannedTime = DateTime.UtcNow.AddHours(1)
            };
            var expected = new PostcodeJourneyPlanResponseModel
            {
                GeneratedAt = DateTime.UtcNow,
                Postcode = "SW1A 1AA",
                StationCrs = "PAD"
            };
            A.CallTo(() => _service.GetPostcodeJourneyPlanAsync(
                request, "SW1A 1AA", "PAD", true))
                .Returns(expected);

            var result = await _controller.Get(request);

            Assert.IsType<OkObjectResult>(result.Result);
            var ok = (OkObjectResult)result.Result;
            Assert.Equal(expected, ok.Value);
        }

        [Fact]
        public async Task Get_WithCrsOriginAndPostcodeDestination_ReturnsOk()
        {
            var request = new PostcodeJourneyPlannerRequest
            {
                Origin = "WAT",
                Destination = "RH6 0NN",
                PlannedTime = DateTime.UtcNow.AddHours(1)
            };
            var expected = new PostcodeJourneyPlanResponseModel
            {
                GeneratedAt = DateTime.UtcNow,
                Postcode = "RH6 0NN",
                StationCrs = "WAT"
            };
            A.CallTo(() => _service.GetPostcodeJourneyPlanAsync(
                request, "RH6 0NN", "WAT", false))
                .Returns(expected);

            var result = await _controller.Get(request);

            Assert.IsType<OkObjectResult>(result.Result);
        }

        [Fact]
        public async Task Get_BothCrs_ReturnsBadRequest_UseStandardPlanner()
        {
            var request = new PostcodeJourneyPlannerRequest
            {
                Origin = "WAT",
                Destination = "PAD",
                PlannedTime = DateTime.UtcNow.AddHours(1)
            };

            var result = await _controller.Get(request);

            Assert.IsType<BadRequestObjectResult>(result.Result);
            var error = (ApiError)((BadRequestObjectResult)result.Result).Value;
            Assert.Equal("USE_STANDARD_PLANNER", error.Code);
        }

        [Fact]
        public async Task Get_BothNonCrs_ReturnsBadRequest_PostcodeNotAllowedForBoth()
        {
            var request = new PostcodeJourneyPlannerRequest
            {
                Origin = "SW1A 1AA",
                Destination = "EC1M 6BY",
                PlannedTime = DateTime.UtcNow.AddHours(1)
            };

            var result = await _controller.Get(request);

            Assert.IsType<BadRequestObjectResult>(result.Result);
            var error = (ApiError)((BadRequestObjectResult)result.Result).Value;
            Assert.Equal("POSTCODE_NOT_ALLOWED_FOR_BOTH", error.Code);
        }

        [Fact]
        public async Task Get_InvalidPostcodeFormat_ReturnsBadRequest()
        {
            var request = new PostcodeJourneyPlannerRequest
            {
                Origin = "NOTAPOSTCODE",
                Destination = "PAD",
                PlannedTime = DateTime.UtcNow.AddHours(1)
            };

            var result = await _controller.Get(request);

            Assert.IsType<BadRequestObjectResult>(result.Result);
            var error = (ApiError)((BadRequestObjectResult)result.Result).Value;
            Assert.Equal("INVALID_POSTCODE_FORMAT", error.Code);
        }

        [Fact]
        public async Task Get_ServiceReturnsNull_Returns502()
        {
            var request = new PostcodeJourneyPlannerRequest
            {
                Origin = "SW1A 1AA",
                Destination = "PAD",
                PlannedTime = DateTime.UtcNow.AddHours(1)
            };
            A.CallTo(() => _service.GetPostcodeJourneyPlanAsync(
                A<PostcodeJourneyPlannerRequest>._, A<string>._, A<string>._, A<bool>._))
                .Returns((PostcodeJourneyPlanResponseModel)null);

            var result = await _controller.Get(request);

            var statusResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(502, statusResult.StatusCode);
            var error = (ApiError)statusResult.Value;
            Assert.Equal("OJP_EMPTY_RESPONSE", error.Code);
        }

        [Fact]
        public async Task Get_OjpFaultNoJourneysFound_Returns404()
        {
            var request = new PostcodeJourneyPlannerRequest
            {
                Origin = "SW1A 1AA",
                Destination = "PAD",
                PlannedTime = DateTime.UtcNow.AddHours(1)
            };
            A.CallTo(() => _service.GetPostcodeJourneyPlanAsync(
                A<PostcodeJourneyPlannerRequest>._, A<string>._, A<string>._, A<bool>._))
                .ThrowsAsync(new OjpFaultException("PostcodeJourneyPlan", "NoJourneysFound", "No journeys available"));

            var result = await _controller.Get(request);

            var statusResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(404, statusResult.StatusCode);
            var error = (ApiError)statusResult.Value;
            Assert.Equal("NoJourneysFound", error.Code);
        }

        [Fact]
        public async Task Get_OjpFaultDateInPast_Returns400()
        {
            var request = new PostcodeJourneyPlannerRequest
            {
                Origin = "SW1A 1AA",
                Destination = "PAD",
                PlannedTime = DateTime.UtcNow.AddHours(1)
            };
            A.CallTo(() => _service.GetPostcodeJourneyPlanAsync(
                A<PostcodeJourneyPlannerRequest>._, A<string>._, A<string>._, A<bool>._))
                .ThrowsAsync(new OjpFaultException("PostcodeJourneyPlan", "OutwardDateTimeInThePast", null));

            var result = await _controller.Get(request);

            var statusResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(400, statusResult.StatusCode);
        }

        [Fact]
        public async Task Get_TimeoutException_Returns504()
        {
            var request = new PostcodeJourneyPlannerRequest
            {
                Origin = "SW1A 1AA",
                Destination = "PAD",
                PlannedTime = DateTime.UtcNow.AddHours(1)
            };
            A.CallTo(() => _service.GetPostcodeJourneyPlanAsync(
                A<PostcodeJourneyPlannerRequest>._, A<string>._, A<string>._, A<bool>._))
                .ThrowsAsync(new TimeoutException("timed out"));

            var result = await _controller.Get(request);

            var statusResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(504, statusResult.StatusCode);
            var error = (ApiError)statusResult.Value;
            Assert.Equal("OJP_TIMEOUT", error.Code);
        }

        [Fact]
        public async Task Get_CommunicationException_Returns502()
        {
            var request = new PostcodeJourneyPlannerRequest
            {
                Origin = "SW1A 1AA",
                Destination = "PAD",
                PlannedTime = DateTime.UtcNow.AddHours(1)
            };
            A.CallTo(() => _service.GetPostcodeJourneyPlanAsync(
                A<PostcodeJourneyPlannerRequest>._, A<string>._, A<string>._, A<bool>._))
                .ThrowsAsync(new CommunicationException("connection failed"));

            var result = await _controller.Get(request);

            var statusResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(502, statusResult.StatusCode);
            var error = (ApiError)statusResult.Value;
            Assert.Equal("OJP_UPSTREAM_COMMUNICATION", error.Code);
        }

        [Fact]
        public async Task Get_OjpUpstreamException_Returns502()
        {
            var request = new PostcodeJourneyPlannerRequest
            {
                Origin = "SW1A 1AA",
                Destination = "PAD",
                PlannedTime = DateTime.UtcNow.AddHours(1)
            };
            A.CallTo(() => _service.GetPostcodeJourneyPlanAsync(
                A<PostcodeJourneyPlannerRequest>._, A<string>._, A<string>._, A<bool>._))
                .ThrowsAsync(new OjpUpstreamException("bad XML"));

            var result = await _controller.Get(request);

            var statusResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(502, statusResult.StatusCode);
            var error = (ApiError)statusResult.Value;
            Assert.Equal("OJP_UPSTREAM_ERROR", error.Code);
        }
    }
}
