using Huxley2.Exceptions;
using Huxley2.Interfaces;
using Huxley2.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NreOJPService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Huxley2.Services
{
    public class PostcodeJourneyPlannerService : IPostcodeJourneyPlannerService
    {
        private readonly ILogger<PostcodeJourneyPlannerService> _logger;
        private readonly IStationService _stationService;
        private readonly HttpClient _httpClient;
        private readonly string _endpoint;
        private readonly string _username;
        private readonly string _password;

        public PostcodeJourneyPlannerService(
            ILogger<PostcodeJourneyPlannerService> logger,
            IStationService stationService,
            HttpClient httpClient,
            IConfiguration config)
        {
            _logger = logger;
            _stationService = stationService;
            _httpClient = httpClient;

            _endpoint = config["ojpEndpoint"]
                ?? config.GetConnectionString("ojpEndpoint")
                ?? throw new InvalidOperationException("Missing ojpEndpoint");
            _username = config["ojpUsername"]
                ?? config.GetConnectionString("ojpUsername")
                ?? throw new InvalidOperationException("Missing ojpUsername");
            _password = config["ojpPassword"]
                ?? config.GetConnectionString("ojpPassword")
                ?? throw new InvalidOperationException("Missing ojpPassword");
        }

        public async Task<PostcodeJourneyPlanResponseModel> GetPostcodeJourneyPlanAsync(
            PostcodeJourneyPlannerRequest request,
            string postcode,
            string stationCrs,
            bool originIsPostcode)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogInformation("Calling OJP raw SOAP PostcodeJourneyPlan");

            try
            {
                var soapXml = BuildSoapRequest(postcode, stationCrs, originIsPostcode, request);
                _logger.LogDebug("OJP SOAP REQUEST (postcode):\n{Soap}", soapXml);

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _endpoint);
                httpRequest.Content = new StringContent(soapXml, Encoding.UTF8, "text/xml");
                httpRequest.Headers.Add("SOAPAction", "\"\"");

                var authBytes = Encoding.ASCII.GetBytes($"{_username}:{_password}");
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using var httpResponse = await _httpClient.SendAsync(httpRequest, cts.Token);

                var responseXml = await httpResponse.Content.ReadAsStringAsync();
                _logger.LogDebug("OJP SOAP RESPONSE (postcode):\n{Soap}", responseXml);

                XDocument doc;
                try
                {
                    doc = XDocument.Parse(responseXml);
                }
                catch (System.Xml.XmlException ex)
                {
                    _logger.LogError(ex, "Failed to parse OJP SOAP response XML (postcode)");
                    throw new OjpUpstreamException("PostcodeJourneyPlan returned invalid XML", ex);
                }

                // Check for SOAP Fault (HTTP 500 from OJP for schema validation errors)
                var faultEl = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Fault");
                if (faultEl != null)
                {
                    var faultString = faultEl.Descendants().FirstOrDefault(e => e.Name.LocalName == "faultstring")?.Value;
                    _logger.LogWarning("OJP SOAP Fault (postcode): {FaultString}", faultString);
                    throw new OjpUpstreamException($"PostcodeJourneyPlan SOAP Fault: {faultString}");
                }

                // Check for PostcodeJourneyPlanFault element (OJP-level fault in body)
                var pcFault = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "PostcodeJourneyPlanFault");
                if (pcFault != null)
                {
                    var faultResponse = pcFault.Descendants().FirstOrDefault(e => e.Name.LocalName == "response")?.Value;
                    var faultDetails = pcFault.Descendants().FirstOrDefault(e => e.Name.LocalName == "responseDetails")?.Value;

                    _logger.LogWarning(
                        "OJP PostcodeJourneyPlanFault. Response={Response}, Details={Details}",
                        faultResponse, faultDetails);

                    throw new OjpFaultException(
                        operation: "PostcodeJourneyPlan",
                        response: faultResponse,
                        responseDetails: faultDetails);
                }

                // Find the PostcodeJourneyPlanResponse element
                var pcResponseEl = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "PostcodeJourneyPlanResponse");
                if (pcResponseEl == null)
                {
                    _logger.LogError("OJP SOAP returned no PostcodeJourneyPlanResponse element");
                    throw new OjpUpstreamException("PostcodeJourneyPlan SOAP response missing PostcodeJourneyPlanResponse element");
                }

                // Check response status
                var responseStatus = pcResponseEl.Elements().FirstOrDefault(e => e.Name.LocalName == "response")?.Value;
                var responseDetails = pcResponseEl.Elements().FirstOrDefault(e => e.Name.LocalName == "responseDetails")?.Value;

                if (responseStatus != null && responseStatus != "Ok")
                {
                    _logger.LogWarning(
                        "OJP SOAP non-Ok response (postcode). Response={Response}, Details={Details}",
                        responseStatus, responseDetails);

                    throw new OjpFaultException(
                        operation: "PostcodeJourneyPlan",
                        response: responseStatus,
                        responseDetails: responseDetails);
                }

                // Parse generatedTime
                var generatedTimeStr = pcResponseEl.Elements().FirstOrDefault(e => e.Name.LocalName == "generatedTime")?.Value;
                var generatedTime = DateTime.TryParse(generatedTimeStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var gt)
                    ? gt : DateTime.UtcNow;

                // Parse postcodeStations
                var postcodeStations = ParsePostcodeStations(pcResponseEl);

                // Parse postcodeResponse entries → journey groups
                var outwardJourneys = ParsePostcodeResponses(pcResponseEl);

                sw.Stop();
                _logger.LogInformation(
                    "OJP SOAP postcode success GeneratedTime={GeneratedTime:o} ElapsedMs={ElapsedMs}",
                    generatedTime, sw.ElapsedMilliseconds);

                return new PostcodeJourneyPlanResponseModel
                {
                    GeneratedAt = generatedTime,
                    PlannedTime = request.PlannedTime,
                    Postcode = postcode,
                    StationCrs = stationCrs,
                    PostcodeStations = postcodeStations,
                    OutwardJourneys = outwardJourneys
                };
            }
            catch (OjpFaultException) { throw; }
            catch (OjpUpstreamException) { throw; }
            catch (TimeoutException) { throw; }
            catch (System.ServiceModel.CommunicationException) { throw; }
            catch (TaskCanceledException ex) when (!ex.CancellationToken.IsCancellationRequested)
            {
                sw.Stop();
                _logger.LogError(ex, "OJP SOAP timeout (postcode) ElapsedMs={ElapsedMs}", sw.ElapsedMilliseconds);
                throw new TimeoutException("OJP PostcodeJourneyPlan request timed out", ex);
            }
            catch (HttpRequestException ex)
            {
                sw.Stop();
                _logger.LogError(ex, "OJP SOAP communication error (postcode) ElapsedMs={ElapsedMs}", sw.ElapsedMilliseconds);
                throw new System.ServiceModel.CommunicationException("OJP PostcodeJourneyPlan communication failure", ex);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "OJP SOAP unexpected error (postcode) ElapsedMs={ElapsedMs}", sw.ElapsedMilliseconds);
                throw;
            }
        }

        private string BuildSoapRequest(string postcode, string stationCrs, bool originIsPostcode, PostcodeJourneyPlannerRequest request)
        {
            string originXml;
            string destinationXml;

            if (originIsPostcode)
            {
                originXml = $@"<jpd:origin>
        <com:postcodeDetails>
          <com:postcode>{EscapeXml(postcode)}</com:postcode>
        </com:postcodeDetails>
      </jpd:origin>";
                destinationXml = $@"<jpd:destination>
        <com:station>
          <com:stationCRS>{EscapeXml(stationCrs)}</com:stationCRS>
        </com:station>
      </jpd:destination>";
            }
            else
            {
                originXml = $@"<jpd:origin>
        <com:station>
          <com:stationCRS>{EscapeXml(stationCrs)}</com:stationCRS>
        </com:station>
      </jpd:origin>";
                destinationXml = $@"<jpd:destination>
        <com:postcodeDetails>
          <com:postcode>{EscapeXml(postcode)}</com:postcode>
        </com:postcodeDetails>
      </jpd:destination>";
            }

            var timeElementName = GetTimeElementName(request.ItemChoiceType);

            // The OJP PostcodeJourneyPlan endpoint treats the time as UK local time
            // (same as the RealtimeJourneyPlan endpoint). No timezone conversion needed —
            // the client sends UK local time and we pass it through as-is.
            var dateTimeStr = request.PlannedTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            var enquiryType = request.EnquiryType == 0 ? "STANDARD" : "CHECK_ALTERNATIVES";
            var directTrains = request.DirectTrains ? "true" : "false";

            return $@"<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:jpd=""http://www.thalesgroup.com/ojp/jpdlr"" xmlns:com=""http://www.thalesgroup.com/ojp/common"">
  <soapenv:Header/>
  <soapenv:Body>
    <jpd:PostcodeJourneyPlanRequest>
      {originXml}
      {destinationXml}
      <jpd:realtimeEnquiry>{enquiryType}</jpd:realtimeEnquiry>
      <jpd:outwardTime>
        <jpd:{timeElementName}>{dateTimeStr}</jpd:{timeElementName}>
      </jpd:outwardTime>
      <jpd:directTrains>{directTrains}</jpd:directTrains>
    </jpd:PostcodeJourneyPlanRequest>
  </soapenv:Body>
</soapenv:Envelope>";
        }

        private static string GetTimeElementName(int itemChoiceType)
        {
            // PostcodeJourneyPlanRequest only supports arriveBy and departBy.
            // Map firstTrainOfDay/lastTrainOfDay to departBy as a safe default.
            return itemChoiceType switch
            {
                0 => "arriveBy",
                1 => "departBy",
                _ => "departBy"
            };
        }

        private static string EscapeXml(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        private List<PostcodeStationModel> ParsePostcodeStations(XElement pcResponseEl)
        {
            var stations = new List<PostcodeStationModel>();
            var stationElements = pcResponseEl.Elements().Where(e => e.Name.LocalName == "postcodeStations");

            foreach (var stEl in stationElements)
            {
                var crs = stEl.Elements().FirstOrDefault(e => e.Name.LocalName == "crsCode")?.Value ?? string.Empty;
                var distStr = stEl.Elements().FirstOrDefault(e => e.Name.LocalName == "distance")?.Value;
                double.TryParse(distStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var distance);

                var station = _stationService.GetStationByCrsCode(crs);
                stations.Add(new PostcodeStationModel
                {
                    Crs = crs,
                    StationName = station?.StationName ?? crs,
                    DistanceMiles = distance
                });
            }

            return stations;
        }

        private List<PostcodeJourneyGroup> ParsePostcodeResponses(XElement pcResponseEl)
        {
            var groups = new List<PostcodeJourneyGroup>();
            var responseElements = pcResponseEl.Elements().Where(e => e.Name.LocalName == "postcodeResponse");

            foreach (var respEl in responseElements)
            {
                var selectedStation = respEl.Elements().FirstOrDefault(e => e.Name.LocalName == "selectedStation")?.Value ?? string.Empty;
                var journeys = new List<OjpJourney>();

                var journeyElements = respEl.Elements().Where(e => e.Name.LocalName == "outwardJourney");
                foreach (var jEl in journeyElements)
                {
                    journeys.Add(ParseJourney(jEl));
                }

                groups.Add(new PostcodeJourneyGroup
                {
                    SelectedStation = selectedStation,
                    Journeys = journeys
                });
            }

            return groups;
        }

        private OjpJourney ParseJourney(XElement journeyEl)
        {
            var idStr = journeyEl.Elements().FirstOrDefault(e => e.Name.LocalName == "id")?.Value;
            int.TryParse(idStr, out var id);

            var origin = journeyEl.Elements().FirstOrDefault(e => e.Name.LocalName == "origin")?.Value ?? string.Empty;
            var destination = journeyEl.Elements().FirstOrDefault(e => e.Name.LocalName == "destination")?.Value ?? string.Empty;
            var rtClassification = journeyEl.Elements().FirstOrDefault(e => e.Name.LocalName == "realtimeClassification")?.Value;

            var timetableEl = journeyEl.Elements().FirstOrDefault(e => e.Name.LocalName == "timetable");
            var journeyTimetable = timetableEl != null ? ParseJourneyTimetable(timetableEl) : null;

            var legs = new List<OjpLeg>();
            var legElements = journeyEl.Elements().Where(e => e.Name.LocalName == "leg");
            foreach (var legEl in legElements)
            {
                legs.Add(ParseLeg(legEl));
            }

            var fares = new List<Fare>();
            var fareElements = journeyEl.Elements().Where(e => e.Name.LocalName == "fare");
            foreach (var fareEl in fareElements)
            {
                fares.Add(ParseFare(fareEl));
            }

            var bulletins = new List<ServiceBulletin>();
            var bulletinElements = journeyEl.Elements().Where(e => e.Name.LocalName == "serviceBulletins");
            foreach (var bEl in bulletinElements)
            {
                bulletins.Add(ParseServiceBulletin(bEl));
            }

            return new OjpJourney
            {
                Id = id,
                OriginStation = _stationService.GetStationByCrsCode(origin),
                DestinationStation = _stationService.GetStationByCrsCode(destination),
                RealTimeClassification = rtClassification,
                JourneyTimetable = journeyTimetable,
                OjpLegs = legs,
                Fare = fares,
                ServiceBulletins = bulletins
            };
        }

        private OjpLeg ParseLeg(XElement legEl)
        {
            var idStr = legEl.Elements().FirstOrDefault(e => e.Name.LocalName == "id")?.Value;
            int.TryParse(idStr, out var id);

            var boardEl = legEl.Elements().FirstOrDefault(e => e.Name.LocalName == "board");
            var board = boardEl?.Elements().FirstOrDefault(e => e.Name.LocalName == "crsCode")?.Value ?? boardEl?.Value ?? string.Empty;
            var alightEl = legEl.Elements().FirstOrDefault(e => e.Name.LocalName == "alight");
            var alight = alightEl?.Elements().FirstOrDefault(e => e.Name.LocalName == "crsCode")?.Value ?? alightEl?.Value ?? string.Empty;

            var origins = legEl.Elements().Where(e => e.Name.LocalName == "origins").Select(e => e.Value).ToArray();
            var destinations = legEl.Elements().Where(e => e.Name.LocalName == "destinations").Select(e => e.Value).ToArray();

            var originPlatform = legEl.Elements().FirstOrDefault(e => e.Name.LocalName == "originPlatform")?.Value;
            var destPlatform = legEl.Elements().FirstOrDefault(e => e.Name.LocalName == "destinationPlatform")?.Value;
            var rtClassification = legEl.Elements().FirstOrDefault(e => e.Name.LocalName == "realtimeClassification")?.Value;
            var mode = legEl.Elements().FirstOrDefault(e => e.Name.LocalName == "mode")?.Value;

            var operatorEl = legEl.Elements().FirstOrDefault(e => e.Name.LocalName == "operator");
            OperatorDetails? operatorDetails = null;
            if (operatorEl != null)
            {
                operatorDetails = ParseOperatorDetails(operatorEl);
            }

            var timetableEl = legEl.Elements().FirstOrDefault(e => e.Name.LocalName == "timetable");
            JourneyLegTimetable? legTimetable = null;
            if (timetableEl != null)
            {
                legTimetable = ParseLegTimetable(timetableEl);
            }

            var undergroundInfo = legEl.Elements()
                .Where(e => e.Name.LocalName == "undergroundTravelInformation")
                .Select(e => e.Value).ToArray();

            return new OjpLeg
            {
                Id = id,
                BoardStation = _stationService.GetStationByCrsCode(board),
                AlightStation = _stationService.GetStationByCrsCode(alight),
                Origins = origins.Length > 0 ? origins : Array.Empty<string>(),
                Destinations = destinations.Length > 0 ? destinations : Array.Empty<string>(),
                OriginPlatform = originPlatform,
                DestinationPlatform = destPlatform,
                RealTimeClassification = rtClassification,
                TravelMode = mode,
                OperatorDetails = operatorDetails,
                JourneyTimetable = legTimetable,
                UndergroundTravelInformation = undergroundInfo.Length > 0 ? undergroundInfo : Array.Empty<string>()
            };
        }

        private static JourneyTimetable ParseJourneyTimetable(XElement timetableEl)
        {
            var tt = new JourneyTimetable();

            var scheduledEl = timetableEl.Elements().FirstOrDefault(e => e.Name.LocalName == "scheduled");
            if (scheduledEl != null)
            {
                tt.scheduled = new JourneyTimetableScheduled();
                var depStr = scheduledEl.Elements().FirstOrDefault(e => e.Name.LocalName == "departure")?.Value;
                var arrStr = scheduledEl.Elements().FirstOrDefault(e => e.Name.LocalName == "arrival")?.Value;
                if (DateTime.TryParse(depStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dep))
                    tt.scheduled.departure = dep;
                if (DateTime.TryParse(arrStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var arr))
                    tt.scheduled.arrival = arr;
            }

            var realtimeEl = timetableEl.Elements().FirstOrDefault(e => e.Name.LocalName == "realtime");
            if (realtimeEl != null)
            {
                tt.realtime = new JourneyTimetableRealtime();
                var depStr = realtimeEl.Elements().FirstOrDefault(e => e.Name.LocalName == "departure")?.Value;
                var arrStr = realtimeEl.Elements().FirstOrDefault(e => e.Name.LocalName == "arrival")?.Value;
                if (DateTime.TryParse(depStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dep))
                {
                    tt.realtime.departure = dep;
                    tt.realtime.departureSpecified = true;
                }
                if (DateTime.TryParse(arrStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var arr))
                {
                    tt.realtime.arrival = arr;
                    tt.realtime.arrivalSpecified = true;
                }
            }

            return tt;
        }

        private static JourneyLegTimetable ParseLegTimetable(XElement timetableEl)
        {
            var tt = new JourneyLegTimetable();

            var scheduledEl = timetableEl.Elements().FirstOrDefault(e => e.Name.LocalName == "scheduled");
            if (scheduledEl != null)
            {
                tt.scheduled = new JourneyLegTimetableScheduled();
                var depStr = scheduledEl.Elements().FirstOrDefault(e => e.Name.LocalName == "departure")?.Value;
                var arrStr = scheduledEl.Elements().FirstOrDefault(e => e.Name.LocalName == "arrival")?.Value;
                if (DateTime.TryParse(depStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dep))
                    tt.scheduled.departure = dep;
                if (DateTime.TryParse(arrStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var arr))
                    tt.scheduled.arrival = arr;
            }

            var realtimeEl = timetableEl.Elements().FirstOrDefault(e => e.Name.LocalName == "realtime");
            if (realtimeEl != null)
            {
                tt.realtime = new JourneyLegTimetableRealtime();
                var depStr = realtimeEl.Elements().FirstOrDefault(e => e.Name.LocalName == "departure")?.Value;
                var arrStr = realtimeEl.Elements().FirstOrDefault(e => e.Name.LocalName == "arrival")?.Value;
                if (DateTime.TryParse(depStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dep))
                {
                    tt.realtime.departure = dep;
                    tt.realtime.departureSpecified = true;
                }
                if (DateTime.TryParse(arrStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var arr))
                {
                    tt.realtime.arrival = arr;
                    tt.realtime.arrivalSpecified = true;
                }
            }

            return tt;
        }

        private static OperatorDetails ParseOperatorDetails(XElement opEl)
        {
            return new OperatorDetails
            {
                code = opEl.Elements().FirstOrDefault(e => e.Name.LocalName == "code")?.Value,
                name = opEl.Elements().FirstOrDefault(e => e.Name.LocalName == "name")?.Value
            };
        }

        private static Fare ParseFare(XElement fareEl)
        {
            var fare = new Fare();

            var idStr = fareEl.Elements().FirstOrDefault(e => e.Name.LocalName == "id")?.Value;
            if (int.TryParse(idStr, out var id)) fare.id = id;

            fare.description = fareEl.Elements().FirstOrDefault(e => e.Name.LocalName == "description")?.Value;

            var totalPriceStr = fareEl.Elements().FirstOrDefault(e => e.Name.LocalName == "totalPrice")?.Value;
            if (int.TryParse(totalPriceStr, out var totalPrice)) fare.totalPrice = totalPrice;

            fare.typeCode = fareEl.Elements().FirstOrDefault(e => e.Name.LocalName == "typeCode")?.Value;
            fare.routeCode = fareEl.Elements().FirstOrDefault(e => e.Name.LocalName == "routeCode")?.Value;
            fare.fareSetter = fareEl.Elements().FirstOrDefault(e => e.Name.LocalName == "fareSetter")?.Value;

            var startLegStr = fareEl.Elements().FirstOrDefault(e => e.Name.LocalName == "startLegId")?.Value;
            if (int.TryParse(startLegStr, out var startLeg)) fare.startLegId = startLeg;

            var endLegStr = fareEl.Elements().FirstOrDefault(e => e.Name.LocalName == "endLegId")?.Value;
            if (int.TryParse(endLegStr, out var endLeg)) fare.endLegId = endLeg;

            return fare;
        }

        private static ServiceBulletin ParseServiceBulletin(XElement bEl)
        {
            var bulletin = new ServiceBulletin();
            bulletin.title = bEl.Elements().FirstOrDefault(e => e.Name.LocalName == "title")?.Value;
            bulletin.description = bEl.Elements().FirstOrDefault(e => e.Name.LocalName == "description")?.Value;
            bulletin.url = bEl.Elements().FirstOrDefault(e => e.Name.LocalName == "url")?.Value;

            var disruptionStr = bEl.Elements().FirstOrDefault(e => e.Name.LocalName == "disruption")?.Value;
            if (bool.TryParse(disruptionStr, out var disruption)) bulletin.disruption = disruption;

            var alertStr = bEl.Elements().FirstOrDefault(e => e.Name.LocalName == "alert")?.Value;
            if (bool.TryParse(alertStr, out var alert)) bulletin.alert = alert;

            var clearedStr = bEl.Elements().FirstOrDefault(e => e.Name.LocalName == "cleared")?.Value;
            if (bool.TryParse(clearedStr, out var cleared)) bulletin.cleared = cleared;

            return bulletin;
        }
    }
}
