# On Rails Proxy Service

A mobile friendly JSON REST proxy for the GB railway Live Departure Boards SOAP API.

Forked from the [Huxley2](https://github.com/jpsingleton/Huxley2) project. The original Huxley 2 is considered feature-complete by its author. This fork by Integrated Software Development Ltd continues active development on the `dev` branch with significant improvements including station locations, nearby services, journey planning, API key authentication, and improved error handling.

[![Buy me a tree!](Huxley2/wwwroot/img/buy-me-a-tree.svg)](https://ecologi.com/unitsetsoftware)

## About

On Rails adds UK national rail station locations, nearby services, journey planning, and API key authentication on top of the Huxley2 proxy service.

The Huxley2 Proxy service connects to the NRE Darwin feed which is an aggregated real-time train feed for all operators in the UK.

Station data is downloaded from [Naptan](http://naptan.app.dft.gov.uk/datarequest/help) and other sources where the Naptan data isn't up to date.

## Get Started

Check out the [live server](https://onrails.azurewebsites.net/) for API documentation.

A Kotlin Multiplatform SDK for iOS and Android is available under a commercial licence. Contact [az@intsoftdev.com](mailto:az@intsoftdev.com) for API keys, SDK access, and integration support.

## API Highlights

### Nearby Services (New)

Get real-time train services from stations near a GPS location in a single call:

```
GET /api/nearby?lat=51.5031&lng=-0.1132&maxStations=3&expand=true
```

Parameters: `lat`, `lng` (required), `destination`, `radius` (default 3, max 50), `maxStations` (default 5, max 20), `numRows` (default 4, max 150), `expand` (default false).

### Journey Planner

Plan journeys between stations via the NRE OJP service:

```
GET /api/planner/{origin}/{destination}/{dateTime}?itemChoiceType=1&enquiryType=0
```

### Station Boards

Departures, arrivals, and more — see the [live docs](https://onrails.azurewebsites.net/) for full details.

### Authentication

All endpoints support API key authentication via the `x-api-key` header. The server supports three modes:

- **Off** — No authentication required
- **Grace** — Unauthenticated requests are logged but allowed through
- **Enforce** — Unauthenticated requests receive 401/403

Configure via `Security__ApiKeyMode` in Azure App Settings.

## Get Your Own

There are detailed instructions on how to host your own instance on Azure in [this blog post](https://unop.uk/huxley-2-release/).

### Configuration

Set these in Azure App Settings (or user secrets for local development):

| Setting | Description |
|---------|-------------|
| `DarwinAccessToken` | NRE access token for the nearby endpoint (server-side calls) |
| `ojpEndpoint` | OJP web service URL |
| `ojpUsername` | NRE OJP username |
| `ojpPassword` | NRE OJP password |
| `Security__ApiKeyMode` | Auth mode: `Off`, `Grace`, or `Enforce` |
| `Security__ApiKeys__0` | API key value (supports multiple keys via `__0`, `__1`, etc.) |
| `Security__ApiKeyHeaderName` | Header name (default: `x-api-key`) |

### Running with Docker

1. Ensure you have Docker and Docker Compose installed
2. Create an `.env` file in the `Huxley2` directory with the access tokens
3. Run `docker-compose up`
4. The app should be available at `localhost:8081`

Example `.env` file:

```env
ACCESS_TOKEN=abcde12345
STAFF_ACCESS_TOKEN=abcde12345
CLIENT_ACCESS_TOKEN=abcde12345
```

To rebuild use `docker-compose build` or `docker-compose up --build`.

## Station Codes File

If you need to regenerate [the station codes CSV file](https://raw.githubusercontent.com/azaka01/huxley2/dev/station_codes.csv) then you can do so with [`jq`](https://stedolan.github.io/jq/) and `curl`:

```bash
curl --silent https://onrails.azurewebsites.net/crs | jq -r '(.[0] | keys_unsorted) as $keys | $keys, map([.[ $keys[] ]])[] | @csv' > station_codes.csv
```

## License

Licensed under the [EUPL-1.2-or-later](https://joinup.ec.europa.eu/collection/eupl/introduction-eupl-licence).

The EUPL covers distribution through a network or SaaS (like a compatible and interoperable AGPL).
