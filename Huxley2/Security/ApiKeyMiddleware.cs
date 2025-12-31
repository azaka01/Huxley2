using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

/*
 *  When Security__ApiKeyMode=Off, this middleware is a no-op.
    When Grace, missing keys are permitted but tagged as legacy.
    When Enforce, missing keys are blocked (401).
    Invalid keys are always blocked (403).
 */

namespace Huxley2.Security
{
    public sealed class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _config;
        private readonly ILogger<ApiKeyMiddleware> _logger;

        public ApiKeyMiddleware(RequestDelegate next, IConfiguration config, ILogger<ApiKeyMiddleware> logger)
        {
            _next = next;
            _config = config;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            var path = context.Request.Path.Value ?? "";
            var traceId = context.TraceIdentifier;

            // Resolve mode (Off | Grace | Enforce)
            var modeStr = _config["Security:ApiKeyMode"] ?? "Off";
            if (!Enum.TryParse<ApiKeyAuthMode>(modeStr, true, out var mode))
                mode = ApiKeyAuthMode.Off;

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

            var headerName = _config["Security:ApiKeyHeaderName"] ?? "x-api-key";
            var hasKeyHeader = context.Request.Headers.TryGetValue(headerName, out var providedValues)
                               && !string.IsNullOrWhiteSpace(providedValues);

            _logger.LogInformation(
                "APIKEY_CHECK mode={Mode} path={Path} requiresKey={RequiresKey} headerPresent={HeaderPresent} traceId={TraceId}",
                mode, path, requiresKey, hasKeyHeader, traceId
            );

            if (mode == ApiKeyAuthMode.Off)
            {
                _logger.LogInformation(
                    "APIKEY_DECISION decision=BypassOff path={Path} traceId={TraceId}",
                    path, traceId
                );

                await _next(context);
                return;
            }

            // Endpoint does not require key → always allow
            if (!requiresKey)
            {
                context.Items["ApiKeyAuthClass"] = ApiKeyAuthClasses.NotRequired;

                _logger.LogInformation(
                    "APIKEY_DECISION decision=AllowNotRequired mode={Mode} path={Path} traceId={TraceId}",
                    mode, path, traceId
                );

                await _next(context);
                return;
            }

            // Missing key (no header or empty)
            if (!hasKeyHeader)
            {
                if (mode == ApiKeyAuthMode.Grace)
                {
                    context.Items["ApiKeyAuthClass"] = ApiKeyAuthClasses.Legacy;

                    _logger.LogInformation(
                        "APIKEY_DECISION decision=AllowGraceMissingHeader mode={Mode} path={Path} traceId={TraceId}",
                        mode, path, traceId
                    );

                    await _next(context);
                    return;
                }

                _logger.LogWarning(
                    "APIKEY_DECISION decision=RejectMissingHeader mode={Mode} path={Path} traceId={TraceId}",
                    mode, path, traceId
                );

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { code = "MISSING_API_KEY", traceId });
                return;
            }

            // Validate key
            var key = providedValues.ToString();
            var validKeys = _config.GetSection("Security:ApiKeys").Get<string[]>() ?? Array.Empty<string>();

            if (validKeys.Length == 0)
            {
                _logger.LogError(
                    "APIKEY_DECISION decision=RejectServerMisconfigNoKeys mode={Mode} path={Path} traceId={TraceId}",
                    mode, path, traceId
                );

                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsJsonAsync(new { code = "API_KEY_NOT_CONFIGURED", traceId });
                return;
            }

            if (!validKeys.Contains(key))
            {
                _logger.LogWarning(
                    "APIKEY_DECISION decision=RejectInvalidKey mode={Mode} path={Path} traceId={TraceId}",
                    mode, path, traceId
                );

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { code = "INVALID_API_KEY", traceId });
                return;
            }

            context.Items["ApiKeyAuthClass"] = ApiKeyAuthClasses.Keyed;

            _logger.LogInformation(
                "APIKEY_DECISION decision=AllowValidKey mode={Mode} path={Path} traceId={TraceId}",
                mode, path, traceId
            );

            await _next(context);
        }
    }
}
