using System.Globalization;

namespace A2G.EndpointProbe.Tool.Cli;

public static class CliParser
{
    public static string HelpText => """
endpoint-probe <url> [options]

Options:
  -u, --url <value>         Endpoint URL to probe.
  -a, --attempts <value>    Number of top-level HTTP attempts. Default: 1.
  -m, --method <value>      HTTP method. Default: GET.
  --headers <value>         Header in 'Name: Value' form. Repeat or use ';' separators.
  --body <value>            Request body for methods such as POST.
  --json                    Emit JSON to stdout.
  --output <path>           Write the rendered output to a file.
  --timeout <value>         Total probe timeout in seconds or hh:mm:ss. Default: 15.
  --insecure                Skip TLS certificate validation.
  -h, --help                Show help.
""";

    public static CliParseResult Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return CliParseResult.Help();
        }

        Uri? url = null;
        var attempts = 1;
        var method = HttpMethod.Get;
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? body = null;
        var json = false;
        string? output = null;
        var timeout = TimeSpan.FromSeconds(15);
        var insecure = false;

        for (var index = 0; index < args.Length; index++)
        {
            var current = args[index];
            switch (current)
            {
                case "-h":
                case "--help":
                    return CliParseResult.Help();
                case "-u":
                case "--url":
                    if (!TryReadValue(args, ref index, out var explicitUrl))
                    {
                        return CliParseResult.Failure("Missing value for --url.");
                    }

                    if (!TryParseUrl(explicitUrl, out url))
                    {
                        return CliParseResult.Failure($"Invalid URL: {explicitUrl}");
                    }

                    break;
                case "-a":
                case "--attempts":
                    if (!TryReadValue(args, ref index, out var attemptsValue) || !int.TryParse(attemptsValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out attempts) || attempts <= 0)
                    {
                        return CliParseResult.Failure("--attempts must be a positive integer.");
                    }

                    break;
                case "-m":
                case "--method":
                    if (!TryReadValue(args, ref index, out var methodValue))
                    {
                        return CliParseResult.Failure("Missing value for --method.");
                    }

                    method = new HttpMethod(methodValue.ToUpperInvariant());
                    break;
                case "--headers":
                    if (!TryReadValue(args, ref index, out var headerValue))
                    {
                        return CliParseResult.Failure("Missing value for --headers.");
                    }

                    foreach (var header in headerValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        var separatorIndex = header.IndexOf(':');
                        if (separatorIndex <= 0)
                        {
                            return CliParseResult.Failure($"Invalid header format: {header}");
                        }

                        var name = header[..separatorIndex].Trim();
                        var value = header[(separatorIndex + 1)..].Trim();
                        headers[name] = value;
                    }

                    break;
                case "--body":
                    if (!TryReadValue(args, ref index, out body))
                    {
                        return CliParseResult.Failure("Missing value for --body.");
                    }

                    break;
                case "--json":
                    json = true;
                    break;
                case "--output":
                    if (!TryReadValue(args, ref index, out output))
                    {
                        return CliParseResult.Failure("Missing value for --output.");
                    }

                    break;
                case "--timeout":
                    if (!TryReadValue(args, ref index, out var timeoutValue) || !TryParseTimeout(timeoutValue, out timeout))
                    {
                        return CliParseResult.Failure("--timeout must be a positive number of seconds or a time span.");
                    }

                    break;
                case "--insecure":
                    insecure = true;
                    break;
                default:
                    if (current.StartsWith("-", StringComparison.Ordinal))
                    {
                        return CliParseResult.Failure($"Unknown option: {current}");
                    }

                    if (url is not null)
                    {
                        return CliParseResult.Failure("Only one URL can be specified.");
                    }

                    if (!TryParseUrl(current, out url))
                    {
                        return CliParseResult.Failure($"Invalid URL: {current}");
                    }

                    break;
            }
        }

        if (url is null)
        {
            return CliParseResult.Failure("A URL is required.");
        }

        if (body is not null && method == HttpMethod.Get)
        {
            method = HttpMethod.Post;
        }

        return CliParseResult.Success(new CliOptions(url, attempts, method, headers, body, json, output, timeout, insecure, false));
    }

    private static bool TryReadValue(string[] args, ref int index, out string value)
    {
        if (index + 1 < args.Length)
        {
            value = args[++index];
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryParseUrl(string value, out Uri? url)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var parsed) && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
        {
            url = parsed;
            return true;
        }

        url = null;
        return false;
    }

    private static bool TryParseTimeout(string value, out TimeSpan timeout)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0)
        {
            timeout = TimeSpan.FromSeconds(seconds);
            return true;
        }

        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out timeout) && timeout > TimeSpan.Zero)
        {
            return true;
        }

        timeout = default;
        return false;
    }
}

