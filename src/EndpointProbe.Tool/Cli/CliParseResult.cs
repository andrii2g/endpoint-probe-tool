using A2G.EndpointProbe.Tool.Models;

namespace A2G.EndpointProbe.Tool.Cli;

public sealed record CliParseResult(
    bool IsSuccess,
    ExecutionMode? Mode,
    CliOptions? ProbeOptions,
    CheckCliOptions? CheckOptions,
    string? Error,
    bool ShowHelp)
{
    public static CliParseResult ProbeSuccess(CliOptions options) => new(true, ExecutionMode.Probe, options, null, null, false);
    public static CliParseResult CheckSuccess(CheckCliOptions options) => new(true, ExecutionMode.Check, null, options, null, false);
    public static CliParseResult Failure(string error) => new(false, null, null, null, error, false);
    public static CliParseResult Help() => new(false, null, null, null, null, true);
}
