namespace A2G.EndpointProbe.Tool.Cli;

public sealed record CliOptions(
    Uri Url,
    int Attempts,
    HttpMethod Method,
    IReadOnlyDictionary<string, string> Headers,
    string? Body,
    bool Json,
    string? Output,
    TimeSpan Timeout,
    bool Insecure,
    bool Help)
{
    public static CliOptions Default(Uri url) => new(
        url,
        Attempts: 1,
        Method: HttpMethod.Get,
        Headers: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        Body: null,
        Json: false,
        Output: null,
        Timeout: TimeSpan.FromSeconds(15),
        Insecure: false,
        Help: false);
}

