# EndpointProbe.Tool

`EndpointProbe.Tool` is a .NET CLI tool for diagnosing HTTP and HTTPS endpoints, including DNS, TLS, timeout, retry, redirect, response consistency, and multi-endpoint readiness checks.

## Run from source

```bash
dotnet run --project src/EndpointProbe.Tool -- https://httpbin.org/json
```

## Install locally

Local install is supported from the packed `.nupkg` output. Publishing to a public package feed is out of scope for v1.

```bash
dotnet pack -c Release -o ./nupkg
dotnet tool install -g EndpointProbe.Tool --add-source ./nupkg
```

## Quick Start

Install the tool locally from the generated package:

```bash
dotnet pack -c Release -o ./nupkg
dotnet tool install -g EndpointProbe.Tool --add-source ./nupkg
```

Probe a JSON endpoint with the default settings:

```bash
endpoint-probe https://httpbin.org/json
```

Run multiple attempts to detect unstable responses:

```bash
endpoint-probe https://httpbin.org/json --attempts 3
```

Inspect a redirect without auto-following it:

```bash
endpoint-probe https://httpbin.org/redirect/1
```

Compare JSON and HTML responses manually:

```bash
endpoint-probe https://httpbin.org/json
endpoint-probe https://httpbin.org/html
```

Emit machine-readable JSON output:

```bash
endpoint-probe https://httpbin.org/json --json
```

Check multiple endpoints directly from the CLI:

```bash
endpoint-probe check https://httpbin.org/status/200 https://httpbin.org/status/500
```

Check endpoints from a config file:

```bash
endpoint-probe check --config endpoints.json
```

Stop after the first failure in CI:

```bash
endpoint-probe check --config endpoints.json --fail-fast
```

Run all checks and return aggregate exit code `5` if any endpoint fails:

```bash
endpoint-probe check --config endpoints.json --full-run
```

Probe from source without installing the tool:

```bash
dotnet run --project src/EndpointProbe.Tool -- https://httpbin.org/json
```

## Check Config

`check --config` expects a JSON array:

```json
[
  {
    "name": "status-ok",
    "url": "https://httpbin.org/status/200",
    "method": "GET",
    "expectedStatus": 200
  },
  {
    "name": "redirect-login",
    "url": "https://httpbin.org/redirect/1",
    "method": "GET",
    "expectedStatus": 302
  }
]
```

## Public Test Targets

For quick manual checks and basic CI validation, `httpbin.org` is useful for deterministic public behaviors such as:

- status codes via `https://httpbin.org/status/<code>`
- redirects via `https://httpbin.org/redirect/1`
- JSON vs HTML content via `https://httpbin.org/json` and `https://httpbin.org/html`
- delayed responses via `https://httpbin.org/delay/3`

Some behaviors still need local test infrastructure or carefully chosen real-world endpoints:

- CDN detection
- fallback/default page detection
- DNS variability
- TLS edge cases

For later planned phases, the same service is a practical validation target for commands such as:

```bash
endpoint-probe wait https://httpbin.org/status/503 --timeout 10
endpoint-probe https://httpbin.org/delay/3 --max-latency-ms 1000
endpoint-probe compare https://httpbin.org/json https://httpbin.org/html
```

Those commands are examples for future phases, not currently available behavior.

## Build

```bash
dotnet restore --configfile NuGet.Config
dotnet build
dotnet test
dotnet pack -c Release -o ./nupkg
```
