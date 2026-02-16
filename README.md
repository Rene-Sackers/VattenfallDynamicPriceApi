# Vattenfall dynamic price scraper & API

**THIS PROJECT IS NOT CREATED, AFFILITED, ENDORSED OR SPONSORED BY VATTENFALL**

Seeing as of the time of writing, there is no official API to get hourly prices from Vattenfall, I have made this application to scrape them off of the following page: https://www.vattenfall.nl/klantenservice/alles-over-je-dynamische-contract/

## Endpoints

| URL                 | Description                                                                                          |
| ------------------- | ---------------------------------------------------------------------------------------------------- |
| /v1/data            | Provides parsed data from Vattenfall                                                                 |
| /v1/evcc            | Provides data compatible with [EVCC](https://docs.evcc.io/en/docs/tariffs#dynamic-electricity-price) |
| /v1/now/electricity | Provides the current price for electricity                                                           |
| /v1/now/gas         | Provides the current price for gas                                                                   |

## Environment variables

You should *not* have to configure these, but they're available just in case.

| Var                          | Default       | Description                                                                                                                                                                                                                                                                  |
| ---------------------------- | ------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| ASPNETCORE_URLS              | http://+:8080 | What ip/port the web app should bind to                                                                                                                                                                                                                                      |
| VFAPI_UseKnownValues         | false         | If the live page ever changes, causing part of the scraping to fail, setting this to true causes the app to use `VFAPI_KnownApiBaseUrl` and `VFAPI_KnownApiKey`. These will need to be manually endered as well. If set to `false`, it will figure it all out automatically. |
| VFAPI_ScrapePageUrl          |               | The page to try and get the API base URL and key from                                                                                                                                                                                                                        |
| VFAPI_KnownApiBaseUrl        |               | The base URL of the API endpoint                                                                                                                                                                                                                                             |
| VFAPI_KnownApiKey            |               | The API key                                                                                                                                                                                                                                                                  |
| VFAPI_RefreshIntervalSeconds | 3600          | The time between API data refreshes                                                                                                                                                                                                                                          |

## Example output data
Endpoint: `/v1/data`

```json
[
  {
    "product": "E",
    "name": "FlexPrijsStroom",
    "productCode": "PPC0014136",
    "tariffData": [
      {
        "startTime": "2026-02-04T00:00:00",
        "endTime": "2026-02-04T01:00:00",
        "isMissingPeriod": false,
        "cheapestOfDay": false,
        "amountInclVat": 0.25,
        "amountExclVat": 0.21,
        "details": [
          {
            "amount": 0.0935,
            "amountExclVat": 0.0935,
            "amountInclVat": 0.11314,
            "type": "PRICE"
          },
          {
            "amount": 0.02107,
            "amountExclVat": 0.02107,
            "amountInclVat": 0.02549,
            "type": "FEE"
          },
          {
            "amount": 0.09161,
            "amountExclVat": 0.09161,
            "amountInclVat": 0.11085,
            "type": "ENERGY_TAX"
          },
          {
            "amount": 0.0433,
            "amountExclVat": 0.0433,
            "amountInclVat": 0,
            "type": "VAT"
          }
        ]
      },
      ... // Rest of the days & hours available
    ],
    "averageTariffs": [
      {
        "date": "2025-09-01T00:00:00",
        "amountInclVat": 0.24,
        "amountExclVat": 0.2
      },
      {
        "date": "2025-10-01T00:00:00",
        "amountInclVat": 0.25,
        "amountExclVat": 0.2
      },
      {
        "date": "2025-11-01T00:00:00",
        "amountInclVat": 0.26,
        "amountExclVat": 0.22
      },
      {
        "date": "2025-12-01T00:00:00",
        "amountInclVat": 0.25,
        "amountExclVat": 0.21
      },
      {
        "date": "2026-01-01T00:00:00",
        "amountInclVat": 0.27,
        "amountExclVat": 0.22
      },
      {
        "date": "2026-02-01T00:00:00",
        "amountInclVat": 0.26,
        "amountExclVat": 0.22
      }
    ]
  },
  ...
]
```

## Docker compose
```yaml
name: vattenfalldynamicpriceapi
services:
  vattenfalldynamicpriceapi:
    container_name: vattenfalldynamicpriceapi
    image: ghcr.io/rene-sackers/vattenfall-dynamic-price-api:latest
    environment:
      - TZ=Europe/Amsterdam
    ports:
      - 8080:8080
```

## Example Home Assistant sensor
```yaml
# configuration.yaml

sensor:
  - platform: rest
    name: "Vattenfall Current Electricity Price"
    resource: https://example.com/v1/now/electricity
    unit_of_measurement: "€/kWh"
    device_class: monetary
    state_class: measurement
    icon: mdi:currency-eur
    scan_interval: 300

  - platform: rest
    name: "Vattenfall Current Gas Price"
    resource: https://example.com/v1/now/gas
    unit_of_measurement: "€/m³"
    device_class: monetary
    state_class: measurement
    icon: mdi:currency-eur
    scan_interval: 300
```

## Example EVCC tariffs config
```yaml
currency: EUR

grid:
  type: custom
  forecast:
    source: http
    uri: https://example.com/v1/evcc
```

## API client

### OpenAPI Spec

The application publishes an OpenAPI spec at: `/openapi/v1.json`. Use this to generate clients.

### .NET
A pre-built .NET API client is available, NuGet feed: `https://nuget.pkg.github.com/Rene-Sackers/index.json`

```
dotnet add package VattenfallDynamicPriceApi.ApiClient
```

Example usage:

```csharp
var authProvider = new AnonymousAuthenticationProvider();
var httpClientRequestAdapter = new HttpClientRequestAdapter(authProvider)
{
	BaseUrl = "https://example.com/"
};
var vattenfallApiClient = new ApiClient(httpClientRequestAdapter);

var data = await vattenfallApiClient.V1.Evcc.GetAsync();
```