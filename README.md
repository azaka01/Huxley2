# On Rails Proxy Service

A mobile friendly JSON REST proxy for the GB railway Live Departure Boards SOAP API.

Forked from the [Huxley2](https://github.com/jpsingleton/Huxley2) project 

[![Buy me a tree!](Huxley2/wwwroot/img/buy-me-a-tree.svg)](https://ecologi.com/unitsetsoftware)

_Note:_ Huxley 2 is considered feature-complete and will only be updated to fix bugs or move to a new .NET LTS version.

## About

On Rails adds stations locations API on top of the Huxley2 Proxy service.

## Get Started

Check out [the live server](https://onrails.azurewebsites.net/) for API documentation

A [mobile client SDK](https://github.com/IntSoftDev/simian) - currently on Android only - is available that facilitates integration with the proxy.

## Get Your Own

There are detailed instructions on how to host your own instance on Azure in [this blog post](https://unop.uk/huxley-2-release/).

### Running with Docker

1. Ensure you have Docker and Docker Compose installed
2. Create an `.env` file in the `Huxley2` directory with the access tokens. You can delete the ones you're not using.
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

If you need to regenerate [the station codes CSV file in this repo](https://raw.githubusercontent.com/jpsingleton/Huxley2/master/station_codes.csv) then you can do so easily with [`jq`](https://stedolan.github.io/jq/) (and `curl`) using an instance that has access to the staff API (and has been restarted recently). On Linux, you can install simply with your package manager, e.g. `sudo apt install jq` (on Ubuntu/Debian).

For example, using the Huxley 2 demo instance you can run this one-liner:

```bash
curl --silent https://huxley2.azurewebsites.net/crs | jq -r '(.[0] | keys_unsorted) as $keys | $keys, map([.[ $keys[] ]])[] | @csv' > station_codes.csv
```

If using a local server with a self-signed certificate:

```bash
curl --silent --insecure https://localhost:5001/crs | jq -r '(.[0] | keys_unsorted) as $keys | $keys, map([.[ $keys[] ]])[] | @csv' > station_codes.csv
```

If you regenerated the station codes CSV file on your own instance, change `StationCodesCsvUrl` in `Huxley2/appsettings.json` to the location of your CSV file.

## License

Licensed under the [EUPL-1.2-or-later](https://joinup.ec.europa.eu/collection/eupl/introduction-eupl-licence).

The EUPL covers distribution through a network or SaaS (like a compatible and interoperable AGPL).
