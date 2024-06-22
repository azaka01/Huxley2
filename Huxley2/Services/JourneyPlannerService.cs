using Huxley2.Interfaces;

        async Task<OjpResponse> IJourneyPlannerService.GetJourneyDetailsAsync(JourneyPlannerRequest request)
        {

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
                            RealTimeClassification = leg.realtimeClassification,
                            Mode = leg.mode,
                            OperatorDetails = leg.@operator,
                            JourneyTimetable = leg.timetable,
                            UndergroundTravelInformation = leg.undergroundTravelInformation
                        });
                    }
                    outwardJourneys.Add(new OjpJourney
                    {
                        Id = rtOutwardJourney.id,
                        OriginStation = originStation,
                        DestinationStation = destinationStation,
                        RealTimeClassification = rtOutwardJourney.realtimeClassification,
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
                        RealTimeClassification = rtInwardJourney.realtimeClassification,
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
            };