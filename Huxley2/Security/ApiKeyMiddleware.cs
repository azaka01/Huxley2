using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Huxley2.Security
{
    public sealed class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IOptionsMonitor<ApiKeyOptions> _options;
        private readonly ILogger<ApiKeyMiddleware> _logger;

        private volatile HashSet<string> _validKeys = new(StringComparer.Ordinal);

        public ApiKeyMiddleware(
            RequestDelegate next,
            IOptionsMonitor<ApiKeyOptions> options,
            ILogger<ApiKeyMiddleware> logger)
        {
            _next = next;
            _options = options;
            _logger = logger;

            RebuildKeyCache(options.CurrentValue);

            _options.OnChange(updated =>
            {
                RebuildKeyCache(updated);
                _logger.LogInformation("APIKEY_CONFIG reloaded keys={KeyCount} mode={Mode} header={Header}",
                    _validKeys.Count, updated.ApiKeyMode, updated.ApiKeyHeaderName);
            });
        }

        private void RebuildKeyCache(ApiKeyOptions opts)
        {
            var keys = opts.ApiKeys ?? Array.Empty<string>();
            var set = new HashSet<string>(StringComparer.Ordinal);

            foreach (var k in keys)
            {
                var key = k?.Trim();
                if (!string.IsNullOrEmpty(key))
                    set.Add(key);
            }

            _validKeys = set;
        }

        public async Task Invoke(HttpContext context)
        {
            var opts = _options.CurrentValue;

            var path = context.Request.Path.Value ?? "";
            var traceId = context.TraceIdentifier;

            var mode = opts.ApiKeyMode;

            context.Response.OnStarting(() =>
            {
                var authClass = context.Items.TryGetValue("ApiKeyAuthClass", out var v) ? v : "NONE";
                _logger.LogInformation(
                    "APIKEY_RESULT status={StatusCode} authClass={AuthClass} mode={Mode} path={Path} traceId={TraceId}",
                    context.Response.StatusCode, authClass, mode, path, traceId);
                return Task.CompletedTask;
            });

            var endpoint = context.GetEndpoint();
            var requiresKey = endpoint?.Metadata.GetMetadata<RequireApiKeyAttribute>() is not null;

            var headerName = string.IsNullOrWhiteSpace(opts.ApiKeyHeaderName) ? "x-api-key" : opts.ApiKeyHeaderName;

            var hasKeyHeader =
                context.Request.Headers.TryGetValue(headerName, out var providedValues) &&
                !string.IsNullOrWhiteSpace(providedValues);

            // ... logging unchanged ...

            if (mode == ApiKeyAuthMode.Off)
            {
                await _next(context);
                return;
            }

            if (!requiresKey)
            {
                context.Items["ApiKeyAuthClass"] = ApiKeyAuthClasses.NotRequired;
                await _next(context);
                return;
            }

            if (!hasKeyHeader)
            {
                if (mode == ApiKeyAuthMode.Grace)
                {
                    context.Items["ApiKeyAuthClass"] = ApiKeyAuthClasses.Legacy;
                    await _next(context);
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { code = "MISSING_API_KEY", traceId });
                return;
            }

            var key = providedValues.Count > 0 ? providedValues[0] : string.Empty;

            if (_validKeys.Count == 0)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsJsonAsync(new { code = "API_KEY_NOT_CONFIGURED", traceId });
                return;
            }

            if (!_validKeys.Contains(key))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { code = "INVALID_API_KEY", traceId });
                return;
            }

            context.Items["ApiKeyAuthClass"] = ApiKeyAuthClasses.Keyed;
            await _next(context);
        }
    }
}
