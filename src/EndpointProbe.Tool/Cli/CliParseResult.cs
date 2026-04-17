namespace A2G.EndpointProbe.Tool.Cli;

public sealed record CliParseResult(bool IsSuccess, CliOptions? Options, string? Error, bool ShowHelp)
{
    public static CliParseResult Success(CliOptions options) => new(true, options, null, false);
    public static CliParseResult Failure(string error) => new(false, null, error, false);
    public static CliParseResult Help() => new(false, null, null, true);
}

