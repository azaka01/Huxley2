namespace Huxley2.Models
{
    public sealed class ApiError
    {
        public string Code { get; init; } = "";
        public string Message { get; init; } = "";
        public string? Details { get; init; }
        public string? TraceId { get; init; }
    }
}
