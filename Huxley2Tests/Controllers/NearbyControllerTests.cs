using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FakeItEasy;
using Huxley2.Controllers;
using Huxley2.Interfaces;
using Huxley2.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpenLDBWS;
using Xunit;

namespace Huxley2Tests.Controllers
{
    public class NearbyControllerTests
    {
        private readonly NearbyController _controller;
        private readonly INearbyStationService _nearbyService;
        private readonly IStationBoardService _boardService;
        private readonly IStationService _stationService;
        private readonly IDateTimeService _dateTimeService;

        public NearbyControllerTests()
        {
            _nearbyService = A.Fake<INearbyStationService>();
            _boardService = A.Fake<IStationBoardService>();
            _stationService = A.Fake<IStationService>();
            _dateTimeService = A.Fake<IDateTimeService>();

            A.CallTo(() => _dateTimeService.UtcNow).Returns(new DateTime(2026, 4, 18, 10, 0, 0, DateTimeKind.Utc));
            A.CallTo(() => _nearbyService.FindNearbyStations(A<double>._, A<double>._, A<double>._, A<int>._))
                .Returns(new List<NearbyStationResult>());

            _controller = new NearbyController(
                _nearbyService,
                _boardService,
                _stationService,
                A.Fake<ILogger<NearbyController>>(),
                _dateTimeService)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };
        }

        // --- Parameter validation: lat ---

        [Fact]
        public async Task Get_MissingLat_Returns400()
        {
            var result = await _controller.Get(null, "-0.1132", null, null, null, null);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Get_NonNumericLat_Returns400()
        {
            var result = await _controller.Get("abc", "-0.1132", null, null, null, null);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Get_LatOutOfRange_Returns400()
        {
            var result = await _controller.Get("91", "-0.1132", null, null, null, null);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Get_LatNegativeOutOfRange_Returns400()
        {
            var result = await _controller.Get("-91", "-0.1132", null, null, null, null);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        // --- Parameter validation: lng ---

        [Fact]
        public async Task Get_MissingLng_Returns400()
        {
            var result = await _controller.Get("51.5", null, null, null, null, null);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Get_NonNumericLng_Returns400()
        {
            var result = await _controller.Get("51.5", "xyz", null, null, null, null);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Get_LngOutOfRange_Returns400()
        {
            var result = await _controller.Get("51.5", "181", null, null, null, null);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        // --- Parameter validation: destination ---

        [Fact]
        public async Task Get_InvalidDestinationFormat_Returns400()
        {
            var result = await _controller.Get("51.5", "-0.1", "X", null, null, null);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Get_DestinationNotFound_Returns400()
        {
            A.CallTo(() => _stationService.GetStationByCrsCode("ZZZ")).Returns(null);
            var result = await _controller.Get("51.5", "-0.1", "ZZZ", null, null, null);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        // --- Parameter validation: radius ---

        [Fact]
        public async Task Get_InvalidRadius_Returns400()
        {
            var result = await _controller.Get("51.5", "-0.1", null, "abc", null, null);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Get_NegativeRadius_Returns400()
        {
            var result = await _controller.Get("51.5", "-0.1", null, "-1", null, null);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        // --- Parameter validation: maxStations ---

        [Fact]
        public async Task Get_InvalidMaxStations_Returns400()
        {
            var result = await _controller.Get("51.5", "-0.1", null, null, "abc", null);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Get_NegativeMaxStations_Returns400()
        {
            var result = await _controller.Get("51.5", "-0.1", null, null, "-1", null);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        // --- Parameter validation: numRows ---

        [Fact]
        public async Task Get_InvalidNumRows_Returns400()
        {
            var result = await _controller.Get("51.5", "-0.1", null, null, null, "abc");
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Get_NegativeNumRows_Returns400()
        {
            var result = await _controller.Get("51.5", "-0.1", null, null, null, "0");
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        // --- Successful request ---

        [Fact]
        public async Task Get_ValidParams_Returns200WithStations()
        {
            var stations = new List<NearbyStationResult>
            {
                new() { StationName = "Waterloo", CrsCode = "WAT", DistanceMiles = 0.1 }
            };
            A.CallTo(() => _nearbyService.FindNearbyStations(51.5, -0.1, 3, 5)).Returns(stations);

            var board = new StationBoard { trainServices = new[] { new ServiceItem1() } };
            A.CallTo(() => _boardService.GetDepartureBoardAsync(A<StationBoardRequest>._)).Returns(board);

            var result = await _controller.Get("51.5", "-0.1", null, null, null, null);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<NearbyServicesResponse>(okResult.Value);
            Assert.Single(response.Stations);
            Assert.Equal("WAT", response.Stations[0].CrsCode);
        }

        [Fact]
        public async Task Get_UsesDefaultRadiusAndMaxStations()
        {
            var result = await _controller.Get("51.5", "-0.1", null, null, null, null);

            A.CallTo(() => _nearbyService.FindNearbyStations(51.5, -0.1, 3, 5))
                .MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task Get_CustomRadiusAndMaxStations()
        {
            var result = await _controller.Get("51.5", "-0.1", null, "5", "10", null);

            A.CallTo(() => _nearbyService.FindNearbyStations(51.5, -0.1, 5, 10))
                .MustHaveHappenedOnceExactly();
        }

        // --- Expand behavior ---

        [Fact]
        public async Task Get_ExpandFalse_RequestsBasicBoard()
        {
            var stations = new List<NearbyStationResult>
            {
                new() { StationName = "Waterloo", CrsCode = "WAT", DistanceMiles = 0.1 }
            };
            A.CallTo(() => _nearbyService.FindNearbyStations(A<double>._, A<double>._, A<double>._, A<int>._)).Returns(stations);
            A.CallTo(() => _boardService.GetDepartureBoardAsync(A<StationBoardRequest>._))
                .Returns(new StationBoard());

            await _controller.Get("51.5", "-0.1", null, null, null, null, expand: false);

            A.CallTo(() => _boardService.GetDepartureBoardAsync(
                A<StationBoardRequest>.That.Matches(r => r.Expand == false)))
                .MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task Get_ExpandTrue_RequestsExpandedBoard()
        {
            var stations = new List<NearbyStationResult>
            {
                new() { StationName = "Waterloo", CrsCode = "WAT", DistanceMiles = 0.1 }
            };
            A.CallTo(() => _nearbyService.FindNearbyStations(A<double>._, A<double>._, A<double>._, A<int>._)).Returns(stations);
            A.CallTo(() => _boardService.GetDepartureBoardAsync(A<StationBoardRequest>._))
                .Returns(new StationBoardWithDetails());

            await _controller.Get("51.5", "-0.1", null, null, null, null, expand: true);

            A.CallTo(() => _boardService.GetDepartureBoardAsync(
                A<StationBoardRequest>.That.Matches(r => r.Expand == true)))
                .MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async Task Get_DestinationSet_ForcesExpand()
        {
            var stations = new List<NearbyStationResult>
            {
                new() { StationName = "Waterloo", CrsCode = "WAT", DistanceMiles = 0.1 }
            };
            A.CallTo(() => _nearbyService.FindNearbyStations(A<double>._, A<double>._, A<double>._, A<int>._)).Returns(stations);
            A.CallTo(() => _stationService.GetStationByCrsCode("CBG")).Returns(new CrsStation { CrsCode = "CBG" });
            A.CallTo(() => _boardService.GetDepartureBoardAsync(A<StationBoardRequest>._))
                .Returns(new StationBoardWithDetails());

            await _controller.Get("51.5", "-0.1", "CBG", null, null, null, expand: false);

            A.CallTo(() => _boardService.GetDepartureBoardAsync(
                A<StationBoardRequest>.That.Matches(r => r.Expand == true)))
                .MustHaveHappenedOnceExactly();
        }

        // --- Destination filtering ---

        [Fact]
        public async Task Get_WithDestination_FiltersServicesByCallingPoints()
        {
            var stations = new List<NearbyStationResult>
            {
                new() { StationName = "Waterloo", CrsCode = "WAT", DistanceMiles = 0.1 }
            };
            A.CallTo(() => _nearbyService.FindNearbyStations(A<double>._, A<double>._, A<double>._, A<int>._)).Returns(stations);
            A.CallTo(() => _stationService.GetStationByCrsCode("CBG")).Returns(new CrsStation { CrsCode = "CBG" });

            var matchingService = new ServiceItemWithCallingPoints
            {
                subsequentCallingPoints = new[]
                {
                    new ArrayOfCallingPoints
                    {
                        callingPoint = new[] { new CallingPoint { crs = "CBG" } }
                    }
                }
            };
            var nonMatchingService = new ServiceItemWithCallingPoints
            {
                subsequentCallingPoints = new[]
                {
                    new ArrayOfCallingPoints
                    {
                        callingPoint = new[] { new CallingPoint { crs = "KGX" } }
                    }
                }
            };

            var board = new StationBoardWithDetails
            {
                trainServices = new[] { matchingService, nonMatchingService }
            };
            A.CallTo(() => _boardService.GetDepartureBoardAsync(A<StationBoardRequest>._)).Returns(board);

            var result = await _controller.Get("51.5", "-0.1", "CBG", null, null, null);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<NearbyServicesResponse>(okResult.Value);
            Assert.Single(response.Stations);
            Assert.Single(response.Stations[0].Services); // only the matching service
        }

        // --- Error handling ---

        [Fact]
        public async Task Get_ServiceFailure_ReturnsStationWithEmptyServices()
        {
            var stations = new List<NearbyStationResult>
            {
                new() { StationName = "Waterloo", CrsCode = "WAT", DistanceMiles = 0.1 }
            };
            A.CallTo(() => _nearbyService.FindNearbyStations(A<double>._, A<double>._, A<double>._, A<int>._)).Returns(stations);
            A.CallTo(() => _boardService.GetDepartureBoardAsync(A<StationBoardRequest>._))
                .Throws(new Exception("Darwin unavailable"));

            var result = await _controller.Get("51.5", "-0.1", null, null, null, null);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<NearbyServicesResponse>(okResult.Value);
            Assert.Single(response.Stations);
            Assert.Empty(response.Stations[0].Services);
        }

        [Fact]
        public async Task Get_NoStationsInRadius_ReturnsEmptyArray()
        {
            A.CallTo(() => _nearbyService.FindNearbyStations(A<double>._, A<double>._, A<double>._, A<int>._))
                .Returns(new List<NearbyStationResult>());

            var result = await _controller.Get("51.5", "-0.1", null, null, null, null);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<NearbyServicesResponse>(okResult.Value);
            Assert.Empty(response.Stations);
        }
    }
}
