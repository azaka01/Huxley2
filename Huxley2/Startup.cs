// © James Singleton. EUPL-1.2 (see the LICENSE file for the full license governing this code).

using Huxley2.Interfaces;
using Huxley2.Models;
using Huxley2.Security;
using Huxley2.Services;
using Huxley2.Soap;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NreOJPService;
using OpenLDBSVWS;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Huxley2
{
    public class Startup
    {
        private readonly IConfiguration _config;
        private readonly bool _enableUpdateCheck;

        private readonly string _endPoint;
        private readonly string _userName;
        private readonly string _password;
        // Unlike WebHost in ASP.NET Core 2, generic Host doesn't support ILogger<T> Startup constructor injection
        // It only supports IHostEnvironment, IWebHostEnvironment, and IConfiguration
        // ILogger<T> can be passed to the Configure method instead
        public Startup(IConfiguration config)
        {
            _config = config;
            _enableUpdateCheck = config.GetValue<bool>("EnableUpdateCheck");
            // these values are configured in secrets.json for local development and AppSettings.json in Azure
            // Prefer App Settings (Environment variables), fallback to ConnectionStrings section.
            _endPoint = config["ojpEndpoint"]
                      ?? config.GetConnectionString("ojpEndpoint")
                      ?? throw new InvalidOperationException("Missing ojpEndpoint (AppSetting) or ConnectionStrings:ojpEndpoint");

            _userName = config["ojpUsername"]
                      ?? config.GetConnectionString("ojpUsername")
                      ?? throw new InvalidOperationException("Missing ojpUsername (AppSetting) or ConnectionStrings:ojpUsername");

            _password = config["ojpPassword"]
                      ?? config.GetConnectionString("ojpPassword")
                      ?? throw new InvalidOperationException("Missing ojpPassword (AppSetting) or ConnectionStrings:ojpPassword");

        }

        public void ConfigureServices(IServiceCollection services)
        {
            // 🔹 Phase 2 prerequisites (add FIRST)
            services.AddMemoryCache();
            services.Configure<RateLimitSettings>(_config.GetSection("Security:RateLimit"));

            services
            .AddOptions<ApiKeyOptions>()
            .Bind(_config.GetSection("Security"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKeyHeaderName),
                "Security:ApiKeyHeaderName is required");

            // Shouldn't be a security issue as plaintext isn't chosen by the user and we aren't using auth or sessions
            // https://docs.microsoft.com/en-us/aspnet/core/performance/response-compression?view=aspnetcore-6.0#compression-with-secure-protocol
            services.AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
            });
            // AddResponseCaching doesn't appear to add any more services but best to be explicit for the future
            services.AddResponseCaching();
            services.AddControllers();
            services.AddRazorPages();
            // ICorsService and ICorsPolicyProvider are already added by AddControllers but best to be explicit
            services.AddCors();

            // Singleton SOAP clients as creation is expensive - DI will take care of calling dispose as it is in a lambda
            services.AddSingleton<OpenLDBWS.LDBServiceSoap, OpenLDBWS.LDBServiceSoapClient>(_ =>
                new OpenLDBWS.LDBServiceSoapClient(OpenLDBWS.LDBServiceSoapClient.EndpointConfiguration.LDBServiceSoap));
            services.AddSingleton<LDBSVServiceSoap, LDBSVServiceSoapClient>(_ =>
                new LDBSVServiceSoapClient(LDBSVServiceSoapClient.EndpointConfiguration.LDBSVServiceSoap));
            services.AddSingleton<LDBSVRefServiceSoap, LDBSVRefServiceSoapClient>(_ =>
                new LDBSVRefServiceSoapClient(LDBSVRefServiceSoapClient.EndpointConfiguration.LDBSVRefServiceSoap));
            services.AddSingleton<jpservices>(sp =>
            {
                var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("OjpSoap");
                return MakeClient(logger);
            });

            services.AddSingleton<IAccessTokenService, AccessTokenService>();
            services.AddSingleton<ICrsService, CrsService>();
            services.AddSingleton<IStationService, CrsStationService>();
            services.AddHostedService<StationWarmupHostedService>();
            services.AddSingleton<IDateTimeService, DateTimeService>();
            services.AddSingleton<IMapperService, MapperService>();
            services.AddSingleton<IStationBoardService, StationBoardService>();
            services.AddSingleton<IStationBoardStaffService, StationBoardStaffService>();
            services.AddSingleton<IDelaysService, DelaysService>();
            services.AddSingleton<IServiceDetailsService, ServiceDetailsService>();
            services.AddSingleton<IUpdateCheckService, UpdateCheckService>();
            services.AddSingleton<IJourneyPlannerService, JourneyPlannerService>();
            services.AddSingleton<INearbyStationService, NearbyStationService>();
            // Singleton HTTP client is best practice and is fine as we don't use authentication or cookies
            // No interface is available but we can mock it by passing in a fake handler to the constructor
            services.AddSingleton<HttpClient>();
        }
        private jpservicesClient MakeClient(ILogger logger)
        {
            var timeout = TimeSpan.FromSeconds(10);

            var client = new jpservicesClient(_endPoint, timeout, _userName, _password);
            client.Endpoint.EndpointBehaviors.Add(new SoapLoggingBehavior(logger));

            return client;
        }

        public void Configure(
            IApplicationBuilder app,
            IWebHostEnvironment env,
            ILogger<Startup> logger,
            IUpdateCheckService updateCheckService)
        {
            // ✅ FIRST — absolute earliest hook into the request
            app.Use(async (context, next) =>
            {
                Console.WriteLine(
                    $"STDOUT HIT {context.Request.Method} {context.Request.Path} TraceId={context.TraceIdentifier}"
                );

                logger.LogInformation(
                    "APPLOG HIT {Method} {Path} TraceId={TraceId}",
                    context.Request.Method,
                    context.Request.Path,
                    context.TraceIdentifier
                );

                context.Response.OnStarting(() =>
                {
                    Console.WriteLine($"STDOUT RESP {context.Response.StatusCode} TraceId={context.TraceIdentifier}");
                    return Task.CompletedTask;
                });

                await next();
            });

            logger.LogInformation("Configuring Huxley 2 web API application");

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            if (env.IsDevelopment())
                logger.LogInformation("OJP endpoint {Endpoint} user {User}", _endPoint, _userName);

            app.UseResponseCompression();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler(errorApp =>
                {
                    errorApp.Run(async context =>
                    {
                        var traceId = context.TraceIdentifier;

                        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                        context.Response.ContentType = "application/json";

                        var payload = new ApiError
                        {
                            Code = "INTERNAL_ERROR",
                            Message = "An unexpected error occurred. Please try again.",
                            TraceId = traceId
                        };

                        await context.Response.WriteAsJsonAsync(payload);
                    });
                });

                app.UseHttpsRedirection();
            }

            app.UseStaticFiles();
            app.UseETagger();

            app.UseRouting();

            app.UseResponseCaching();

            // CORS must be called after UseRouting and before UseEndpoints to function correctly
            // The `Access-Control-Allow-Origin` header will not be added to normal GET responses
            // An `Origin` header must be on the request (for a different domain) for CORS to run
            // https://docs.microsoft.com/en-us/aspnet/core/security/cors
            app.UseCors(config => config.AllowAnyOrigin());

            // API key middleware MUST run after UseRouting so endpoint metadata is available,
            // and before UseEndpoints so it can block/allow.
            app.UseMiddleware<ApiKeyMiddleware>();
            app.UseMiddleware<AuthClassRateLimitMiddleware>();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapRazorPages();
            });

            logger.LogInformation("Huxley 2 web API application configured");

            try
            { 
                if (_enableUpdateCheck)
                {
                    logger.LogInformation("Checking for any available updates to Huxley");
                    updateCheckService.CheckForUpdates().GetAwaiter().GetResult();
                }
            }
            catch (UpdateCheckServiceException e)
            {
                logger.LogError(e, "Non-fatal startup failure");
            }

            logger.LogInformation("Huxley 2 web API application ready");
        }
    }
}
