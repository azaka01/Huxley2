// © James Singleton. EUPL-1.2 (see the LICENSE file for the full license governing this code).

using Huxley2.Interfaces;
using Huxley2.Services;
using Huxley2.Soap;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NreOJPService;
using OpenLDBSVWS;
using System;
using System.Net.Http;

namespace Huxley2
{
    public class Startup
    {
        private readonly bool _enableUpdateCheck;

        private static string endPoint = "";
        private static string userName = "";
        private static string password = "";
        // Unlike WebHost in ASP.NET Core 2, generic Host doesn't support ILogger<T> Startup constructor injection
        // It only supports IHostEnvironment, IWebHostEnvironment, and IConfiguration
        // ILogger<T> can be passed to the Configure method instead
        public Startup(IConfiguration config)
        {
            _enableUpdateCheck = config.GetValue<bool>("EnableUpdateCheck");
            // these values are configured in secrets.json for local development and AppSettings.json in Azure
            // Prefer App Settings (Environment variables), fallback to ConnectionStrings section.
            endPoint = config["ojpEndpoint"]
                      ?? config.GetConnectionString("ojpEndpoint")
                      ?? throw new InvalidOperationException("Missing ojpEndpoint (AppSetting) or ConnectionStrings:ojpEndpoint");

            userName = config["ojpUsername"]
                      ?? config.GetConnectionString("ojpUsername")
                      ?? throw new InvalidOperationException("Missing ojpUsername (AppSetting) or ConnectionStrings:ojpUsername");

            password = config["ojpPassword"]
                      ?? config.GetConnectionString("ojpPassword")
                      ?? throw new InvalidOperationException("Missing ojpPassword (AppSetting) or ConnectionStrings:ojpPassword");

        }

        public static void ConfigureServices(IServiceCollection services)
        {

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
                return makeClient(logger);
            });

            services.AddSingleton<IAccessTokenService, AccessTokenService>();
            services.AddSingleton<ICrsService, CrsService>();
            services.AddSingleton<IStationService, CrsStationService>();
            services.AddSingleton<IDateTimeService, DateTimeService>();
            services.AddSingleton<IMapperService, MapperService>();
            services.AddSingleton<IStationBoardService, StationBoardService>();
            services.AddSingleton<IStationBoardStaffService, StationBoardStaffService>();
            services.AddSingleton<IDelaysService, DelaysService>();
            services.AddSingleton<IServiceDetailsService, ServiceDetailsService>();
            services.AddSingleton<IUpdateCheckService, UpdateCheckService>();
            services.AddSingleton<IJourneyPlannerService, JourneyPlannerService>();
            // Singleton HTTP client is best practice and is fine as we don't use authentication or cookies
            // No interface is available but we can mock it by passing in a fake handler to the constructor
            services.AddSingleton<HttpClient>();
        }

        private static jpservicesClient makeClient(ILogger logger)
        {
            TimeSpan timeout = TimeSpan.FromSeconds(10);

            var client = new jpservicesClient(endPoint, timeout, userName, password);

            // Attach WCF endpoint behavior for raw SOAP logging
            client.Endpoint.EndpointBehaviors.Add(new SoapLoggingBehavior(logger));

            return client;
        }

        public void Configure(
    IApplicationBuilder app,
    IWebHostEnvironment env,
    ILogger<Startup> logger,
    ICrsService crsService,
    IStationService stationService,
    IUpdateCheckService updateCheckService)
        {
            logger.LogInformation("Configuring Huxley 2 web API application");

            if (env.IsDevelopment())
                logger.LogInformation("OJP endpoint {Endpoint} user {User}", endPoint, userName);

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

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapRazorPages();
            });

            logger.LogInformation("Huxley 2 web API application configured");

            try
            {
                logger.LogInformation("Loading CRS station codes from remote source");

                crsService.LoadCrsCodes().GetAwaiter().GetResult();
                stationService.LoadStations().GetAwaiter().GetResult();

                if (_enableUpdateCheck)
                {
                    logger.LogInformation("Checking for any available updates to Huxley");
                    updateCheckService.CheckForUpdates().GetAwaiter().GetResult();
                }
            }
            catch (Exception e) when (e is CrsServiceException || e is UpdateCheckServiceException)
            {
                logger.LogError(e, "Non-fatal startup failure");
            }

            logger.LogInformation("Huxley 2 web API application ready");
        }
    }
}
