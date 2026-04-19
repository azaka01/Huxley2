# AI-Assisted Development

This document serves as an instructional context for the Huxley2 web service project.

## Project Context

- ASP.NET Core (.NET 6) REST proxy for the GB railway Live Departure Boards SOAP API
- Converts NRE Darwin SOAP/XML responses to JSON
- Also proxies the NRE Online Journey Planner (OJP) SOAP API
- Station data loaded from Naptan CSV + a JSON addendum file for new/missing stations
- Deployed to Azure App Service (Windows)

## Architecture

- Controllers are thin wrappers — validation + delegation to services
- SOAP clients are generated via Connected Services (OpenLDBWS, OpenLDBSVWS, NreOJPService)
- `ApiKeyMiddleware` handles authentication with Off/Grace/Enforce modes
- `ETagMiddleware` adds response caching via ETags
- Station data is loaded at startup by `CrsStationService` and cached in memory

## Key Patterns

- Use `[RequireApiKey]` attribute on controllers that need auth. Auth is configurable via `Security__ApiKeyMode` — set to `Off` to run without auth, `Grace` to log but allow unauthenticated requests, or `Enforce` to require the `x-api-key` header. Only endpoints with `[RequireApiKey]` are affected; the attribute can be omitted for public endpoints
- Use `IStationService` for station lookups (includes lat/lng from CrsStation)
- Use `IAccessTokenService` for Darwin token resolution — supports both URL token and server-configured token
- OJP faults are captured by `SoapLoggingBehaviour` and mapped to HTTP status codes via `MapOjpFault` in PlannerController
- New stations are added to `stations_addendum.json`, not the CSV

## Testing

- Unit tests use xUnit + FakeItEasy
- Tests run in Visual Studio (CLI build has a pre-existing project reference issue)
- Test server: https://onrails-test.azurewebsites.net
- Production: https://onrails.azurewebsites.net

## API Documentation

Full API documentation including endpoint details, parameters, examples, authentication, credentials, and licensing is available on the live server home page: https://onrails.azurewebsites.net

The documentation is served from `Huxley2/Pages/Index.cshtml` and covers all endpoints (station boards, nearby services, journey planner, CRS codes, station locations, service details), authentication modes, credential requirements, and the hosted service offering.

## Deployment

- Publish via Visual Studio Web Deploy
- IMPORTANT: Do NOT use "Remove additional files at destination" — it deletes the `config/` folder on the server
- The `config/stationsversion.json` file lives on the server only, not in the repo
- Set `DarwinAccessToken` in Azure App Settings for the nearby endpoint to work
- `Security__ApiKeyMode` controls auth: `Off`, `Grace`, or `Enforce`
