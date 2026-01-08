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

| Var                   | Default       | Description                                                                                                                                                                                                                                                                 |
| --------------------- | ------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| ASPNETCORE_URLS       | http://+:8080 | What ip/port the web app should bind to                                                                                                                                                                                                                                     |
| VFAPI_UseKnownValues  | false         | If the live page ever changes, causing part of the scraping to fail, setting this to true causes the app to use `VFAPI_KnownApiBaseUrl` and `VFAPI_KnownApiKey`. These will need to be manually endered as well. If set to `true`, it will figure it all out automatically. |
| VFAPI_ScrapePageUrl   |               | The page to try and get the API base URL and key from                                                                                                                                                                                                                       |
| VFAPI_KnownApiBaseUrl |               | The base URL of the API endpoint                                                                                                                                                                                                                                            |
| VFAPI_KnownApiKey     |               | The API key                                                                                                                                                                                                                                                                 |

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