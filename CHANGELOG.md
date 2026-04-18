# Changelog

## 1.2.0

- add multi-endpoint `check` mode with config, fail-fast, full-run, and aggregate exit behavior

## 1.1.0

- upgrade to `System.CommandLine`
- upgrade tests to xUnit v3
- improve TLS diagnostics by separating certificate validation failures from transport failures and preserving connect/handshake timings on failure
- surface redirect responses without auto-following them and include redirect details in console and JSON output
- verify packed tool installation in GitHub Actions

## 1.0.0

Initial release with the `endpoint-probe` .NET tool, xUnit tests, packaging metadata, and GitHub Actions workflows for build, test, and pack.
