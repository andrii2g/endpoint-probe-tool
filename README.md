# EndpointProbe.Tool

`EndpointProbe.Tool` is a .NET CLI tool for diagnosing HTTP and HTTPS endpoints, including DNS, TLS, timeout, retry, redirect, and response consistency issues.

## Run from source

```bash
dotnet run --project src/EndpointProbe.Tool -- https://example.com
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

Probe an endpoint with the default settings:

```bash
endpoint-probe https://example.com
```

Run multiple attempts to detect unstable responses:

```bash
endpoint-probe https://example.com --attempts 3
```

Inspect a redirect without auto-following it:

```bash
endpoint-probe https://example.com/login
```

Emit machine-readable JSON output:

```bash
endpoint-probe https://example.com --json
```

Probe from source without installing the tool:

```bash
dotnet run --project src/EndpointProbe.Tool -- https://example.com
```

## Build

```bash
dotnet restore --configfile NuGet.Config
dotnet build
dotnet test
dotnet pack -c Release -o ./nupkg
```
