using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using System.Xml;
using System.Xml.Linq;

namespace Huxley2.Soap
{
    public sealed class SoapLoggingInspector : IClientMessageInspector
    {
        private readonly ILogger _logger;

        public SoapLoggingInspector(ILogger logger) => _logger = logger;

        public object BeforeSendRequest(ref Message request, System.ServiceModel.IClientChannel channel)
        {
            // New: reset context per call
            OjpCallContextAccessor.Current.Value = new OjpCallContext();

            try
            {
                var soap = MessageToString(ref request);
                _logger.LogInformation("OJP SOAP REQUEST:\n{Soap}", soap);
                _logger.LogInformation("OJP SOAP ENDPOINT: {RemoteAddress}", channel.RemoteAddress?.Uri?.ToString());
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogWarning(ex, "Failed to log SOAP request");
            }
            catch (System.ServiceModel.CommunicationException ex)
            {
                _logger.LogWarning(ex, "Failed to log SOAP request");
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Failed to log SOAP request");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Failed to log SOAP request");
            }

            return null!;
        }

        public void AfterReceiveReply(ref Message reply, object correlationState)
        {
            try
            {
                var soap = MessageToString(ref reply);
                _logger.LogInformation("OJP SOAP RESPONSE:\n{Soap}", soap);

                // New: capture fault details for service layer
                CaptureFaultIfPresent(soap);
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogWarning(ex, "Failed to log SOAP response");
            }
            catch (System.ServiceModel.CommunicationException ex)
            {
                _logger.LogWarning(ex, "Failed to log SOAP response");
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Failed to log SOAP response");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Failed to log SOAP response");
            }
        }

        private static void CaptureFaultIfPresent(string soap)
        {
            var ctx = OjpCallContextAccessor.Current.Value;
            if (ctx == null) return;

            // Keep raw soap optional; if logs already include it, you can omit storing it
            // ctx.RawSoap = soap;

            if (soap.Contains("RealtimeJourneyPlanFault", StringComparison.Ordinal))
            {
                ctx.Operation = "RealtimeJourneyPlan";
                ctx.FaultCode = ExtractFaultDetailValue(soap, "response");
                ctx.FaultDetails = ExtractFaultDetailValue(soap, "responseDetails");
                return;
            }

            if (soap.Contains("RealtimeCallingPointsFault", StringComparison.Ordinal))
            {
                ctx.Operation = "RealtimeCallingPoints";
                ctx.FaultCode = ExtractFaultDetailValue(soap, "response");
                ctx.FaultDetails = ExtractFaultDetailValue(soap, "responseDetails");
                return;
            }

            if (soap.Contains("PostcodeJourneyPlanFault", StringComparison.Ordinal))
            {
                ctx.Operation = "PostcodeJourneyPlan";
                ctx.FaultCode = ExtractFaultDetailValue(soap, "response");
                ctx.FaultDetails = ExtractFaultDetailValue(soap, "responseDetails");
            }
        }

        private static string? ExtractFaultDetailValue(string xml, string localTagName)
        {
            if (string.IsNullOrWhiteSpace(xml)) return null;

            try
            {
                var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);

                // First try: standard SOAP Fault/detail structure
                var fault = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Fault");
                if (fault != null)
                {
                    var detail = fault.Descendants().FirstOrDefault(e => e.Name.LocalName == "detail");
                    if (detail != null)
                    {
                        var el = detail.Descendants().FirstOrDefault(e =>
                            e.Name.LocalName.Equals(localTagName, StringComparison.Ordinal));
                        if (el?.Value != null) return el.Value.Trim();
                    }
                }

                // Second try: OJP-style fault directly in SOAP Body (e.g. RealtimeJourneyPlanFault/response)
                var ojpFault = doc.Descendants().FirstOrDefault(e =>
                    e.Name.LocalName.EndsWith("Fault", StringComparison.Ordinal));
                if (ojpFault != null)
                {
                    var el = ojpFault.Descendants().FirstOrDefault(e =>
                        e.Name.LocalName.Equals(localTagName, StringComparison.Ordinal));
                    if (el?.Value != null) return el.Value.Trim();
                }

                return null;
            }
            catch (XmlException)
            {
                return null;
            }
        }

        private const int SoapBufferSizeBytes = 1024 * 1024; // 1 MB

        private static string MessageToString(ref Message message)
        {
            var buffer = message.CreateBufferedCopy(SoapBufferSizeBytes);
            var copy = buffer.CreateMessage();
            message = buffer.CreateMessage();
            return copy.ToString();
        }
    }

    public sealed class SoapLoggingBehavior : IEndpointBehavior
    {
        private readonly ILogger _logger;

        public SoapLoggingBehavior(ILogger logger) => _logger = logger;

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            clientRuntime.ClientMessageInspectors.Add(new SoapLoggingInspector(_logger));
        }

        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters) { }
        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher) { }
        public void Validate(ServiceEndpoint endpoint) { }
    }
}
