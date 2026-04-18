using System.Diagnostics;
using A2G.EndpointProbe.Tool.Cli;
using A2G.EndpointProbe.Tool.Models;

namespace A2G.EndpointProbe.Tool.Services;

public sealed class CheckExecutor(
    IEndpointProbeService endpointProbeService,
    CheckConfigLoader configLoader)
{
    public async Task<CheckRunReport> ExecuteAsync(CheckCliOptions options, CancellationToken cancellationToken)
    {
        var definitions = await ResolveDefinitionsAsync(options, cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        var results = new List<EndpointCheckResult>(definitions.Count);

        foreach (var definition in definitions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var endpointStopwatch = Stopwatch.StartNew();
            var probeOptions = new CliOptions(
                definition.Url,
                options.Attempts,
                new HttpMethod(definition.Method),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                null,
                options.Json,
                options.Output,
                options.Timeout,
                options.Insecure,
                Help: false);

            var probe = await endpointProbeService.ProbeAsync(probeOptions, cancellationToken);
            endpointStopwatch.Stop();

            results.Add(CreateResult(definition, probe, endpointStopwatch.Elapsed.TotalMilliseconds));
            if (options.FailFast && !results[^1].Passed)
            {
                break;
            }
        }

        stopwatch.Stop();

        var failedNames = results.Where(result => !result.Passed).Select(result => result.Name).ToArray();
        var passedCount = results.Count - failedNames.Length;
        var summary = new CheckRunSummary(
            results.Count,
            passedCount,
            failedNames.Length,
            stopwatch.Elapsed.TotalMilliseconds,
            failedNames,
            failedNames.Length == 0,
            failedNames.Length == 0 ? ExitCodeValue.Success : ExitCodeValue.Unstable);

        return new CheckRunReport(
            ExecutionMode.Check,
            options.FailFast,
            options.FullRun,
            definitions.Count,
            results,
            summary);
    }

    private async Task<IReadOnlyList<EndpointCheckDefinition>> ResolveDefinitionsAsync(CheckCliOptions options, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.ConfigPath))
        {
            var config = await configLoader.LoadAsync(options.ConfigPath, cancellationToken);
            return config.Endpoints;
        }

        return options.Endpoints;
    }

    private static EndpointCheckResult CreateResult(EndpointCheckDefinition definition, ProbeResult probe, double durationMs)
    {
        var actualStatusCode = probe.Attempts.FirstOrDefault(attempt => attempt.Succeeded)?.StatusCode;
        var failureReason = GetFailureReason(definition, probe, actualStatusCode, out var passed);

        return new EndpointCheckResult(
            definition.Name,
            definition,
            passed,
            actualStatusCode,
            durationMs,
            failureReason,
            probe);
    }

    private static string? GetFailureReason(EndpointCheckDefinition definition, ProbeResult probe, int? actualStatusCode, out bool passed)
    {
        if (probe.Summary.ExitCode != ExitCodeValue.Success)
        {
            passed = false;
            return probe.Summary.Notes is null
                ? probe.Summary.Verdict
                : $"{probe.Summary.Verdict}: {probe.Summary.Notes}";
        }

        if (actualStatusCode != definition.ExpectedStatus)
        {
            passed = false;
            return actualStatusCode is null
                ? $"expected {definition.ExpectedStatus}"
                : $"expected {definition.ExpectedStatus}, got {actualStatusCode}";
        }

        passed = true;
        return null;
    }
}
