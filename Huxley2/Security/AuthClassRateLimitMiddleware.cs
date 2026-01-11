using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace Huxley2.Security
{
    /// <summary>
    /// Rate limits Planner traffic differently for legacy vs keyed clients.
    /// Depends on ApiKeyMiddleware setting context.Items["ApiKeyAuthClass"].
    /// </summary>
    public sealed class AuthClassRateLimitMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IMemoryCache _cache;
        private readonly ILogger<AuthClassRateLimitMiddleware> _logger;
        private readonly RateLimitSettings _settings;

        public AuthClassRateLimitMiddleware(
            RequestDelegate next,
            IMemoryCache cache,
            IOptions<RateLimitSettings> settings,
            ILogger<AuthClassRateLimitMiddleware> logger)
        {
            _next = next;
            _cache = cache;
            _logger = logger;
            _settings = settings.Value;
        }

        public async Task Invoke(HttpContext context)
        {
            if (!_settings.Enabled)
            {
                await _next(context);
                return;
            }

            // Optional hard scope: Planner only (safest initial rollout)
            // If your planner endpoints have a different prefix, change it here.
            var path = context.Request.Path.Value ?? "";
            if (!path.StartsWith("/api/planner", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            // Do not rate-limit CORS preflight requests
            if (HttpMethods.IsOptions(context.Request.Method))
            {
                await _next(context);
                return;
            }

            var authClass =
                context.Items.TryGetValue("ApiKeyAuthClass", out var value)
                && value is string s
                    ? s
                    : "NONE";

            // If ApiKeyMiddleware tagged not_required, don't throttle by default
            // (keeps RL tightly scoped to keyed/legacy flows).
            if (authClass == ApiKeyAuthClasses.NotRequired)
            {
                await _next(context);
                return;
            }

            var now = DateTimeOffset.UtcNow;

            // Partitioning strategy:
            // - keyed: per (hashed) api key when present
            // - missing/legacy: shared bucket (or per client IP for non-keyed flows)
            // Note: ApiKeyMiddleware doesn't store the raw key in Items (good),
            // so for keyed we read from headers and hash it.
            if (authClass == ApiKeyAuthClasses.Keyed)
            {
                // Use the same header name as ApiKeyMiddleware (default x-api-key)
                // If missing, still treat as keyed partition = "missing"
                var apiKeyHeaderName = "x-api-key";
                if (context.RequestServices.GetService(typeof(Microsoft.Extensions.Configuration.IConfiguration))
                    is Microsoft.Extensions.Configuration.IConfiguration cfg)
                {
                    apiKeyHeaderName = cfg["Security:ApiKeyHeaderName"] ?? "x-api-key";
                }

                var apiKey = context.Request.Headers[apiKeyHeaderName].ToString();

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    // Missing key: treat as legacy/anonymous bucket (stricter).
                    if (!TryConsume("rl:LEGACY:missing", _settings.Legacy.PermitLimit, TimeSpan.FromSeconds(_settings.Legacy.WindowSeconds), now, out var retryAfter))
                    {
                        Reject429(context, ApiKeyAuthClasses.Legacy, retryAfter);
                        return;
                    }
                }
                else
                {
                    var partition = GetPartitionForApiKey(apiKey);
                    if (!TryConsume($"rl:KEYED:{partition}", _settings.Keyed.PermitLimit, TimeSpan.FromSeconds(_settings.Keyed.WindowSeconds), now, out var retryAfter))
                    {
                        Reject429(context, authClass, retryAfter);
                        return;
                    }
                }
            }
            else
            {
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                if (!TryConsume($"rl:LEGACY:{ip}", _settings.Legacy.PermitLimit, TimeSpan.FromSeconds(_settings.Legacy.WindowSeconds), now, out var retryAfter))
                {
                    Reject429(context, authClass, retryAfter);
                    return;
                }
            }

            await _next(context);
        }

        private string GetPartitionForApiKey(string apiKey)
        {
            var partition = _cache.GetOrCreate("rl:PART:" + apiKey, entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromHours(6);
                return Sha256Hex(apiKey);
            });

            return partition!;
        }
        private bool TryConsume(string cacheKey, int limit, TimeSpan window, DateTimeOffset now, out int retryAfterSeconds)
        {
            retryAfterSeconds = 0;

            var counter = _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = window;
                return new Counter
                {
                    WindowStart = now,
                    Count = 0,
                    WindowSeconds = (int)window.TotalSeconds
                };
            });

            lock (counter)
            {
                counter.Count++;

                if (counter.Count <= limit)
                    return true;

                var resetAt = counter.WindowStart.AddSeconds(counter.WindowSeconds);
                var remaining = (int)Math.Ceiling((resetAt - now).TotalSeconds);
                retryAfterSeconds = Math.Clamp(remaining, 1, counter.WindowSeconds);
                return false;
            }
        }

        private void Reject429(HttpContext context, string authClass, int retryAfterSeconds)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.Headers["Retry-After"] = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

            _logger.LogWarning(
                "RATE_LIMIT_REJECT status=429 authClass={AuthClass} path={Path} traceId={TraceId} retryAfter={RetryAfterSeconds}s",
                authClass,
                context.Request.Path.Value ?? "",
                context.TraceIdentifier,
                retryAfterSeconds);
        }

        private static string Sha256Hex(string value)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value));
            return Convert.ToHexString(bytes);
        }

        private sealed class Counter
        {
            public DateTimeOffset WindowStart { get; set; }
            public int Count { get; set; }
            public int WindowSeconds { get; set; }
        }
    }
}
