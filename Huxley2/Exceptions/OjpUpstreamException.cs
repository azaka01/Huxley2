using System;
using System.Runtime.Serialization;

namespace Huxley2.Exceptions
{
    [Serializable]
    public sealed class OjpUpstreamException : Exception
    {
        public OjpUpstreamException()
        {
        }

        public OjpUpstreamException(string message)
            : base(message)
        {
        }

        public OjpUpstreamException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected OjpUpstreamException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
    }
}
