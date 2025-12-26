using System;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.ServiceModel.Dispatcher;
using Microsoft.Extensions.Logging;

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
        }

        private static void CaptureFaultIfPresent(string soap)
        {
            var ctx = OjpCallContextAccessor.Current.Value;
            if (ctx == null) return;

            // Keep raw soap optional; if logs already include it, you can omit storing it
            // ctx.RawSoap = soap;

            // RealtimeJourneyPlan fault
            if (soap.Contains("RealtimeJourneyPlanFault", StringComparison.Ordinal))
            {
                ctx.Operation = "RealtimeJourneyPlan";
                ctx.FaultCode = ExtractTagValue(soap, "response");
                ctx.FaultDetails = ExtractTagValue(soap, "responseDetails");
                return;
            }

            // Calling points fault (if you want the same handling there)
            if (soap.Contains("RealtimeCallingPointsFault", StringComparison.Ordinal))
            {
                ctx.Operation = "RealtimeCallingPoints";
                ctx.FaultCode = ExtractTagValue(soap, "response");
                ctx.FaultDetails = ExtractTagValue(soap, "responseDetails");
            }
        }

        private static string? ExtractTagValue(string xml, string localTagName)
        {
            // Find the start tag: <ns:tag ...> or <tag ...>
            var idx = xml.IndexOf("<" + localTagName, StringComparison.Ordinal);
            var prefixedIdx = xml.IndexOf(":" + localTagName, StringComparison.Ordinal);

            if (prefixedIdx >= 0)
            {
                var lt = xml.LastIndexOf('<', prefixedIdx);
                if (lt >= 0) idx = (idx < 0) ? lt : Math.Min(idx, lt);
            }

            if (idx < 0) return null;

            var gt = xml.IndexOf('>', idx);
            if (gt < 0) return null;

            // Self-closing: <tag .../>
            if (gt > 0 && xml[gt - 1] == '/')
                return string.Empty;

            // Find corresponding end tag (we accept any prefix)
            var end = xml.IndexOf("</", gt + 1, StringComparison.Ordinal);
            if (end < 0) return null;

            return xml.Substring(gt + 1, end - (gt + 1)).Trim();
        }


        private static string MessageToString(ref Message message)
        {
            var buffer = message.CreateBufferedCopy(int.MaxValue);
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
