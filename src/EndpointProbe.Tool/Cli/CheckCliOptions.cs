using A2G.EndpointProbe.Tool.Models;

namespace A2G.EndpointProbe.Tool.Cli;

public sealed record CheckCliOptions(
    string? ConfigPath,
    IReadOnlyList<EndpointCheckDefinition> Endpoints,
    int Attempts,
    bool Json,
    string? Output,
    TimeSpan Timeout,
    bool Insecure,
    bool FailFast)
{
    public bool FullRun => !FailFast;
}
