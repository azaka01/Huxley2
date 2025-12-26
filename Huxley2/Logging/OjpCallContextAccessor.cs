using System.Threading;

namespace Huxley2.Soap
{
    public sealed class OjpCallContext
    {
        public string? Operation { get; set; }
        public string? FaultCode { get; set; }
        public string? FaultDetails { get; set; }
        public string? RawSoap { get; set; } // optional, keep small or omit in prod
    }

    public static class OjpCallContextAccessor
    {
        public static readonly AsyncLocal<OjpCallContext?> Current = new();
    }
}
