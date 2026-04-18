namespace A2G.EndpointProbe.Tool.Models;

public sealed record CheckConfig(IReadOnlyList<EndpointCheckDefinition> Endpoints);

public sealed record EndpointCheckDefinition(
    string Name,
    Uri Url,
    string Method,
    int ExpectedStatus)
{
    public static string CreateDefaultName(Uri url)
    {
        var authority = url.IsDefaultPort ? url.Host : $"{url.Host}:{url.Port}";
        var path = url.AbsolutePath == "/" ? string.Empty : url.AbsolutePath.TrimEnd('/');
        return string.IsNullOrWhiteSpace(path)
            ? authority
            : $"{authority}{path}";
    }
}

public sealed record EndpointCheckResult(
    string Name,
    EndpointCheckDefinition Definition,
    bool Passed,
    int? ActualStatusCode,
    double DurationMs,
    string? FailureReason,
    ProbeResult Probe);

public sealed record CheckRunSummary(
    int TotalEndpoints,
    int PassedEndpoints,
    int FailedEndpoints,
    double ElapsedMs,
    IReadOnlyList<string> FailedEndpointNames,
    bool Succeeded,
    ExitCodeValue ExitCode);

public sealed record CheckRunReport(
    ExecutionMode Mode,
    bool FailFast,
    bool FullRun,
    int ConfiguredEndpoints,
    IReadOnlyList<EndpointCheckResult> Results,
    CheckRunSummary Summary);
