using System;
using System.Runtime.Serialization;

namespace Huxley2.Exceptions
{
    [Serializable]
    public sealed class OjpFaultException : Exception
    {
        public string? Operation { get; }
        public string? FaultCode { get; }
        public string? FaultDetails { get; }

        public OjpFaultException()
        {
        }

        public OjpFaultException(string message)
            : base(message)
        {
        }

        public OjpFaultException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public OjpFaultException(
            string operation,
            string? response,
            string? responseDetails)
            : base($"{operation} SOAP fault: {response} - {responseDetails}")
        {
            Operation = operation;
            FaultCode = response;
            FaultDetails = responseDetails;
        }

        protected OjpFaultException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
            Operation = info.GetString(nameof(Operation));
            FaultCode = info.GetString(nameof(FaultCode));
            FaultDetails = info.GetString(nameof(FaultDetails));
        }

        public override void GetObjectData(
            SerializationInfo info,
            StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(nameof(Operation), Operation);
            info.AddValue(nameof(FaultCode), FaultCode);
            info.AddValue(nameof(FaultDetails), FaultDetails);
        }
    }
}
