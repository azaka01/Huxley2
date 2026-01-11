using Huxley2.Security;
using System;

public sealed class ApiKeyOptions
{
    public ApiKeyAuthMode ApiKeyMode { get; init; } = ApiKeyAuthMode.Off;
    public string ApiKeyHeaderName { get; init; } = "x-api-key";
    public string[] ApiKeys { get; init; } = Array.Empty<string>();
}
