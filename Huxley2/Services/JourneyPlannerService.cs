using Huxley2.Interfaces;using Microsoft.Extensions.Logging;using NreOJPService;using System.Threading.Tasks;using Huxley2.Models;using System.Collections.Generic;using System;namespace Huxley2.Services{    public class JourneyPlannerService : IJourneyPlannerService    {        private readonly ILogger<JourneyPlannerService> _logger;        private readonly IMapperService _mapperService;        private readonly IStationService _stationService;        private readonly jpservices _jpClient;        public JourneyPlannerService(            ILogger<JourneyPlannerService> logger,            IStationService stationService,            IMapperService mapperService,            jpservices jpClient        )        {            _logger = logger;            _stationService = stationService;            _mapperService = mapperService;            _jpClient = jpClient;        }

        async Task<OjpCallingPointsResponse> IJourneyPlannerService.GetJourneyCallingPointsAsync(JourneyCallingPointsRequest request)
        {
            var response = await _jpClient.RealtimeCallingPointsAsync(                    _mapperService.MapGetCallingPointsRequest(request)                    );
            var usableResponse = response.RealtimeCallingPointsResponse;
            // usableResponse could be null

            if (usableResponse != null)
            {
                var ojpCpLegs = new List<OjpCallingPointLeg>();

                foreach (var leg in usableResponse.leg)
                {
                    var singlePointLegs = new List<OjpSingleCallingPointLeg>();

                    foreach (var singleLeg in leg.realtimeCallingPoint)
                    {
                        singlePointLegs.Add(new OjpSingleCallingPointLeg()
                        {
                            Station = _stationService.GetStationByCrsCode(singleLeg.station),
                            Platform = singleLeg.platform,
                            ArrivalTime = singleLeg.timetable.scheduledTimes.arrival,
                            DepartureTime = singleLeg.timetable.scheduledTimes.departure,
                            Board = singleLeg.board,
                            Alight = singleLeg.alight,
                            StationDelayOrCancelData = GetDelayOrCancelData(singleLeg)
                        });
                    };

                    ojpCpLegs.Add(new OjpCallingPointLeg
                    {
                        Id = leg.id,
                        OriginStation = _stationService.GetStationByCrsCode(leg.origin),
                        DestinationStation = _stationService.GetStationByCrsCode(leg.destination),
                        ArrivalTime = leg.arrival,
                        DepartureTime = leg.departure,
                        SearchStatus = leg.searchStatus.ToString(),
                        OjpSingleCpLegs = singlePointLegs
                    });
                }

                return new OjpCallingPointsResponse
                {
                    GeneratedAt = usableResponse.generatedTime,
                    OriginStation = _stationService.GetStationByCrsCode(usableResponse.origin),
                    DestinationStation = _stationService.GetStationByCrsCode(usableResponse.destination),
                    OjpCpLegs = ojpCpLegs
                };
            } else
            {
                return new OjpCallingPointsResponse
                {
                    // TODO include status field that no repsonse was received
                };
            }
        }

        private static OjpCallingPointDelayOrCancelData? GetDelayOrCancelData(RealtimeCallingPointsResponseLegRealtimeCallingPoint response)
        {
            if (response.realtime == null) return null;

            var updatedArrivalTime = GetArrivalTimeFromResponse(response.realtime.updatedTimes);
            var updatedDepartureTime = GetDepartureTimeFromResponse(response.realtime.updatedTimes);

            return new OjpCallingPointDelayOrCancelData
            {
                UpdatedArrivalTime = updatedArrivalTime,
                UpdatedDepartureTime = updatedDepartureTime,
                IsCancelled = response.realtime.cancelled,
                CancellationReason = response.realtime.cancellationReason,
                DelayedReason = response.realtime.lateRunningReason
            };
        }

        private static DateTime? GetArrivalTimeFromResponse(RealtimeCallingPointsResponseLegRealtimeCallingPointRealtimeUpdatedTimes response)
        {
            if (response == null || response.arrival == null)
            {
                return null;
            }

            return response.arrival.time;
        }

        private static DateTime? GetDepartureTimeFromResponse(RealtimeCallingPointsResponseLegRealtimeCallingPointRealtimeUpdatedTimes response)
        {
            if (response == null || response.departure == null)
            {
                return null;
            }

            return response.departure.time;
        }

        async Task<OjpResponse> IJourneyPlannerService.GetJourneyDetailsAsync(JourneyPlannerRequest request)
        {            _logger.LogInformation($"Calling GetJourneyDetailsAsync SOAP endpoint");            var response = await _jpClient.RealtimeJourneyPlanAsync(                    _mapperService.MapGetJourneyPlannerRequest(request)                    );

            var rtResponse = response.RealtimeJourneyPlanResponse;
            // TODO RealtimeJourneyPlanFault not sure how to handle
            // RealtimeJourneyPlanResponse will be null if SOAP API error

            var outwardJourneys = new List<OjpJourney>();

            if (rtResponse.outwardJourney != null)
            {
                foreach (var rtOutwardJourney in rtResponse.outwardJourney)
                {
                    var originStation = _stationService.GetStationByCrsCode(rtOutwardJourney.origin);
                    var destinationStation = _stationService.GetStationByCrsCode(rtOutwardJourney.destination);
                    var ojpOutwardLegs = new List<OjpLeg>();
                    foreach (var leg in rtOutwardJourney.leg)
                    {
                        ojpOutwardLegs.Add(new OjpLeg
                        {
                            Id = leg.id,
                            BoardStation = _stationService.GetStationByCrsCode(leg.board),
                            AlightStation = _stationService.GetStationByCrsCode(leg.alight),
                            Origins = leg.origins,
                            Destinations = leg.destinations,
                            OriginPlatform = leg.originPlatform,
                            DestinationPlatform = leg.destinationPlatform,
                            RealTimeClassification = leg.realtimeClassification.ToString(),
                            TravelMode = leg.mode.ToString(),
                            OperatorDetails = leg.@operator,
                            JourneyTimetable = leg.timetable,
                            UndergroundTravelInformation = leg.undergroundTravelInformation
                        }); ;
                    }
                    outwardJourneys.Add(new OjpJourney
                    {
                        Id = rtOutwardJourney.id,
                        OriginStation = originStation,
                        DestinationStation = destinationStation,
                        RealTimeClassification = rtOutwardJourney.realtimeClassification.ToString(),
                        JourneyTimetable = rtOutwardJourney.timetable,
                        OjpLegs = ojpOutwardLegs,
                        Fare = rtOutwardJourney.fare,
                        ServiceBulletins = rtOutwardJourney.serviceBulletins
                    });
                }
            }

            var inwardJourneys = new List<OjpJourney>();
            if (rtResponse.inwardJourney != null)
            {
                foreach (var rtInwardJourney in rtResponse.inwardJourney)
                {
                    var originStation = _stationService.GetStationByCrsCode(rtInwardJourney.origin);
                    var destinationStation = _stationService.GetStationByCrsCode(rtInwardJourney.destination);
                    var ojpInwardLegs = new List<OjpLeg>();
                    foreach (var leg in rtInwardJourney.leg)
                    {
                        ojpInwardLegs.Add(new OjpLeg
                        {
                            Id = leg.id,
                            BoardStation = _stationService.GetStationByCrsCode(leg.board),
                            AlightStation = _stationService.GetStationByCrsCode(leg.alight),
                            Origins = leg.origins,
                            Destinations = leg.destinations,
                            OriginPlatform = leg.originPlatform,
                            DestinationPlatform = leg.destinationPlatform,
                            OperatorDetails = leg.@operator,
                            JourneyTimetable = leg.timetable,
                            UndergroundTravelInformation = leg.undergroundTravelInformation
                        });
                    }
                    inwardJourneys.Add(new OjpJourney
                    {
                        Id = rtInwardJourney.id,
                        OriginStation = originStation,
                        DestinationStation = destinationStation,
                        RealTimeClassification = rtInwardJourney.realtimeClassification.ToString(),
                        JourneyTimetable = rtInwardJourney.timetable,
                        OjpLegs = ojpInwardLegs,
                        Fare = rtInwardJourney.fare,
                        ServiceBulletins = rtInwardJourney.serviceBulletins
                    });
                }
            }

            return new OjpResponse
            {
                GeneratedAt = rtResponse.generatedTime,
                OutwardJourneys = outwardJourneys,
                InwardJourneys = inwardJourneys,
                NrsStatus = rtResponse.nrsStatus,
                Response = rtResponse.response,
                ResponseDetails = rtResponse.responseDetails
            };        }    }}