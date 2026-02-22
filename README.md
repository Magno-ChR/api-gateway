# API Gateway (DP)

API Gateway built with **.NET**, **YARP** (Yet Another Reverse Proxy), **Serilog** (logging), and **Polly** (circuit breaker + retry). All incoming requests that match configured routes are forwarded to backend services; logs are in English.

---

## How it works

1. **Client** sends a request to the gateway (e.g. `http://localhost:5001/api/Authentications`).
2. **Gateway** receives it and runs middleware (exception handling, request logging, Serilog request logging).
3. **YARP** matches the request path against the **Routes** defined in `appsettings.json`. If there is a match, the request is forwarded to the **Cluster** (backend) linked to that route.
4. **Polly** (via `PollyForwarderHttpClientFactory`) applies **retry** (2 attempts with exponential backoff) and **circuit breaker** (opens after 3 failures for 15 seconds) to outbound calls to backends.
5. The **backend** responds; the gateway returns that response to the client.

No route match means the request is handled by the gateway’s own controllers (e.g. `WeatherForecast`) or returns 404.

**Project layout:**

- **Extensions** – Serilog, YARP registration, middleware registration.
- **Middleware** – Request logging, global exception handling.
- **Proxy** – `PollyForwarderHttpClientFactory` (YARP HTTP client with Polly policies).
- **Controllers** – Gateway’s own endpoints (e.g. sample WeatherForecast).

---

## Adding new routes (other APIs)

Routing is **only** configured in `appsettings.json` under the **ReverseProxy** section. No code changes are needed to add or change routes.

### 1. New API, new backend URL

Add a **route** and a **cluster** with a new destination:

```json
"ReverseProxy": {
  "Routes": {
    "security-auth": {
      "ClusterId": "security-api",
      "Match": { "Path": "/api/Authentications" }
    },
    "security-users": {
      "ClusterId": "security-api",
      "Match": { "Path": "/api/Users/{**catch-all}" }
    },
    "my-new-api-route": {
      "ClusterId": "my-new-api",
      "Match": { "Path": "/api/orders/{**catch-all}" }
    }
  },
  "Clusters": {
    "security-api": {
      "Destinations": {
        "security-backend": {
          "Address": "http://localhost:5000"
        }
      }
    },
    "my-new-api": {
      "Destinations": {
        "orders-backend": {
          "Address": "http://localhost:6000"
        }
      }
    }
  }
}
```

- **Route key** (e.g. `my-new-api-route`): any unique name.
- **ClusterId**: must match a key under **Clusters**.
- **Match.Path**:
  - Exact path: `"/api/Orders"`.
  - Path + rest: `"/api/Orders/{**catch-all}"` (e.g. `/api/Orders`, `/api/Orders/123`, `/api/Orders/123/items`).
- **Clusters**: each cluster has **Destinations** with **Address** = backend base URL (no trailing path).

After editing `appsettings.json`, restart the gateway. The new route will forward to the new API with the same Polly behavior (retry + circuit breaker).

### 2. Same API, extra path

If the backend is the same (e.g. still `http://localhost:5000`), add only a **route** and reuse the existing **cluster**:

```json
"Routes": {
  "security-auth": { "ClusterId": "security-api", "Match": { "Path": "/api/Authentications" } },
  "security-users": { "ClusterId": "security-api", "Match": { "Path": "/api/Users/{**catch-all}" } },
  "security-reports": { "ClusterId": "security-api", "Match": { "Path": "/api/Reports/{**catch-all}" } }
}
```

No new cluster is needed; `security-reports` will use `security-api` and the same backend.

### 3. Path transformation (prefix)

To expose the backend under a different path (e.g. gateway `/reports` → backend `/api/Reports`), use **Transforms**:

```json
"reports-route": {
  "ClusterId": "reports-api",
  "Match": { "Path": "/reports/{**catch-all}" },
  "Transforms": [
    { "PathPrefix": "-/reports" },
    { "PathPrefix": "/api/Reports" }
  ]
}
```

- First transform strips the `/reports` prefix.
- Second adds `/api/Reports`, so the backend sees `/api/Reports/...`.

---

## Running the gateway

- **Port**: by default the gateway listens on **http://localhost:5001** (see `Properties/launchSettings.json` and `.vscode/launch.json`).
- **Backend**: ensure the Security API (or any backend you route to) is running, e.g. at `http://localhost:5000` for the default config.

```bash
cd api-gateway-dp
dotnet run
```

Example through the gateway (Security API):

```bash
curl --location 'http://localhost:5001/api/Authentications' \
  --header 'Content-Type: application/json' \
  --data '{"username":"Prueba","password":"1234"}'
```

More examples are in `api-gateway-dp.api-gateway-dp.http`.

---

## Tech stack

| Component        | Role                                      |
|-----------------|-------------------------------------------|
| **YARP**        | Reverse proxy; routes and clusters in config |
| **Polly**       | Retry + circuit breaker on backend calls |
| **Serilog**     | Structured logging to console (English)  |
| **Middleware**  | Request logging, global exception handling |
