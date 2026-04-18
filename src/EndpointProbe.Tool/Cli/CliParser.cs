using System.CommandLine;
using System.Globalization;
using System.Text.RegularExpressions;
using A2G.EndpointProbe.Tool.Models;

namespace A2G.EndpointProbe.Tool.Cli;

public static partial class CliParser
{
    private static readonly Regex HttpMethodPattern = CreateHttpMethodPattern();

    public static string HelpText => """
endpoint-probe <url> [options]
endpoint-probe check (--config <path> | <url> [<url> ...]) [options]

Probe options:
  -u, --url <value>         Endpoint URL to probe.
  -a, --attempts <value>    Number of top-level HTTP attempts. Default: 1.
  -m, --method <value>      HTTP method. Default: GET.
  --headers <value>         Header in 'Name: Value' form. Repeat or use ';' separators.
  --body <value>            Request body for methods such as POST.
  --json                    Emit JSON to stdout.
  --output <path>           Write the rendered output to a file.
  --timeout <value>         Total probe timeout in seconds or hh:mm:ss. Default: 15.
  --insecure                Skip TLS certificate validation.

Check options:
  --config <path>           Load endpoint checks from a JSON array config file.
  --fail-fast               Stop after the first failing endpoint.
  --full-run                Continue through all configured endpoints (default).

General:
  -h, --help                Show help.
""";

    public static CliParseResult Parse(string[] args)
    {
        if (args.Length == 0 || args.Any(IsHelpToken))
        {
            return CliParseResult.Help();
        }

        return string.Equals(args[0], "check", StringComparison.OrdinalIgnoreCase)
            ? ParseCheck(args[1..])
            : ParseProbe(args);
    }

    private static CliParseResult ParseProbe(string[] args)
    {
        var definition = CreateProbeDefinition();
        var parseResult = definition.RootCommand.Parse(args);
        if (parseResult.Errors.Count > 0)
        {
            return CliParseResult.Failure(parseResult.Errors[0].Message);
        }

        var positionalUrl = parseResult.GetValue(definition.UrlArgument);
        var optionUrl = parseResult.GetValue(definition.UrlOption);
        if (!string.IsNullOrWhiteSpace(positionalUrl) && !string.IsNullOrWhiteSpace(optionUrl))
        {
            return CliParseResult.Failure("Specify the URL either positionally or with --url, not both.");
        }

        var rawUrl = optionUrl ?? positionalUrl;
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return CliParseResult.Failure("A URL is required.");
        }

        if (!TryParseUrl(rawUrl, out var url))
        {
            return CliParseResult.Failure($"Invalid URL: {rawUrl}");
        }

        var attempts = parseResult.GetValue(definition.AttemptsOption) ?? 1;
        if (attempts <= 0)
        {
            return CliParseResult.Failure("--attempts must be a positive integer.");
        }

        var methodName = parseResult.GetValue(definition.MethodOption) ?? "GET";
        if (!IsValidHttpMethod(methodName))
        {
            return CliParseResult.Failure($"Invalid HTTP method: {methodName}");
        }

        var method = new HttpMethod(methodName.ToUpperInvariant());

        var timeoutText = parseResult.GetValue(definition.TimeoutOption);
        var timeout = TimeSpan.FromSeconds(15);
        if (timeoutText is not null && !TryParseTimeout(timeoutText, out timeout))
        {
            return CliParseResult.Failure("--timeout must be a positive number of seconds or a time span.");
        }

        if (!TryParseHeaders(parseResult.GetValue(definition.HeadersOption) ?? [], out var headers, out var headerError))
        {
            return CliParseResult.Failure(headerError!);
        }

        var body = parseResult.GetValue(definition.BodyOption);
        if (body is not null && method == HttpMethod.Get)
        {
            method = HttpMethod.Post;
        }

        return CliParseResult.ProbeSuccess(new CliOptions(
            url!,
            attempts,
            method,
            headers,
            body,
            parseResult.GetValue(definition.JsonOption),
            parseResult.GetValue(definition.OutputOption),
            timeout,
            parseResult.GetValue(definition.InsecureOption),
            Help: false));
    }

    private static CliParseResult ParseCheck(string[] args)
    {
        var definition = CreateCheckDefinition();
        var parseResult = definition.Command.Parse(args);
        if (parseResult.Errors.Count > 0)
        {
            return CliParseResult.Failure(parseResult.Errors[0].Message);
        }

        var attempts = parseResult.GetValue(definition.AttemptsOption) ?? 1;
        if (attempts <= 0)
        {
            return CliParseResult.Failure("--attempts must be a positive integer.");
        }

        var timeoutText = parseResult.GetValue(definition.TimeoutOption);
        var timeout = TimeSpan.FromSeconds(15);
        if (timeoutText is not null && !TryParseTimeout(timeoutText, out timeout))
        {
            return CliParseResult.Failure("--timeout must be a positive number of seconds or a time span.");
        }

        var failFast = parseResult.GetValue(definition.FailFastOption);
        var fullRun = parseResult.GetValue(definition.FullRunOption);
        if (failFast && fullRun)
        {
            return CliParseResult.Failure("Specify either --fail-fast or --full-run, not both.");
        }

        var method = parseResult.GetValue(definition.MethodOption);
        var body = parseResult.GetValue(definition.BodyOption);
        var headers = parseResult.GetValue(definition.HeadersOption) ?? [];
        if (!string.IsNullOrWhiteSpace(method) || !string.IsNullOrWhiteSpace(body) || headers.Length > 0)
        {
            return CliParseResult.Failure("--method, --headers, and --body are not supported in check mode. Use a config file to define per-endpoint request details.");
        }

        var configPath = parseResult.GetValue(definition.ConfigOption);
        var rawUrls = parseResult.GetValue(definition.UrlsArgument) ?? [];
        if (!string.IsNullOrWhiteSpace(configPath) && rawUrls.Length > 0)
        {
            return CliParseResult.Failure("Specify endpoints with either --config or direct URLs, not both.");
        }

        if (string.IsNullOrWhiteSpace(configPath) && rawUrls.Length == 0)
        {
            return CliParseResult.Failure("check mode requires either --config <path> or one or more URLs.");
        }

        var endpoints = new List<EndpointCheckDefinition>(rawUrls.Length);
        foreach (var rawUrl in rawUrls)
        {
            if (!TryParseUrl(rawUrl, out var url))
            {
                return CliParseResult.Failure($"Invalid URL: {rawUrl}");
            }

            endpoints.Add(new EndpointCheckDefinition(
                EndpointCheckDefinition.CreateDefaultName(url!),
                url!,
                "GET",
                200));
        }

        return CliParseResult.CheckSuccess(new CheckCliOptions(
            configPath,
            endpoints,
            attempts,
            parseResult.GetValue(definition.JsonOption),
            parseResult.GetValue(definition.OutputOption),
            timeout,
            parseResult.GetValue(definition.InsecureOption),
            FailFast: failFast));
    }

    private static ProbeCommandDefinition CreateProbeDefinition()
    {
        var urlArgument = new Argument<string?>("url")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = "Endpoint URL to probe."
        };

        var urlOption = new Option<string?>("--url", ["-u"]) { Description = "Endpoint URL to probe." };
        var attemptsOption = new Option<int?>("--attempts", ["-a"]) { Description = "Number of top-level HTTP attempts." };
        var methodOption = new Option<string?>("--method", ["-m"]) { Description = "HTTP method." };
        var headersOption = new Option<string[]>("--headers")
        {
            Description = "Header in 'Name: Value' form. Repeat or use ';' separators.",
            AllowMultipleArgumentsPerToken = true
        };
        var bodyOption = new Option<string?>("--body") { Description = "Request body for methods such as POST." };
        var jsonOption = new Option<bool>("--json") { Description = "Emit JSON to stdout." };
        var outputOption = new Option<string?>("--output") { Description = "Write the rendered output to a file." };
        var timeoutOption = new Option<string?>("--timeout") { Description = "Total probe timeout in seconds or hh:mm:ss." };
        var insecureOption = new Option<bool>("--insecure") { Description = "Skip TLS certificate validation." };

        var rootCommand = new RootCommand("Probe HTTP and HTTPS endpoints for DNS, TLS, timeout, retry, and response consistency issues.");
        rootCommand.Add(urlArgument);
        rootCommand.Add(urlOption);
        rootCommand.Add(attemptsOption);
        rootCommand.Add(methodOption);
        rootCommand.Add(headersOption);
        rootCommand.Add(bodyOption);
        rootCommand.Add(jsonOption);
        rootCommand.Add(outputOption);
        rootCommand.Add(timeoutOption);
        rootCommand.Add(insecureOption);

        return new ProbeCommandDefinition(rootCommand, urlArgument, urlOption, attemptsOption, methodOption, headersOption, bodyOption, jsonOption, outputOption, timeoutOption, insecureOption);
    }

    private static CheckCommandDefinition CreateCheckDefinition()
    {
        var urlsArgument = new Argument<string[]>("urls")
        {
            Arity = ArgumentArity.ZeroOrMore,
            Description = "One or more endpoint URLs to check."
        };

        var configOption = new Option<string?>("--config") { Description = "Load endpoint checks from a JSON array config file." };
        var failFastOption = new Option<bool>("--fail-fast") { Description = "Stop after the first failing endpoint." };
        var fullRunOption = new Option<bool>("--full-run") { Description = "Continue through all configured endpoints." };
        var attemptsOption = new Option<int?>("--attempts", ["-a"]) { Description = "Number of top-level HTTP attempts per endpoint." };
        var methodOption = new Option<string?>("--method", ["-m"]) { Description = "Not supported in check mode." };
        var headersOption = new Option<string[]>("--headers")
        {
            Description = "Not supported in check mode.",
            AllowMultipleArgumentsPerToken = true
        };
        var bodyOption = new Option<string?>("--body") { Description = "Not supported in check mode." };
        var jsonOption = new Option<bool>("--json") { Description = "Emit JSON to stdout." };
        var outputOption = new Option<string?>("--output") { Description = "Write the rendered output to a file." };
        var timeoutOption = new Option<string?>("--timeout") { Description = "Total probe timeout in seconds or hh:mm:ss." };
        var insecureOption = new Option<bool>("--insecure") { Description = "Skip TLS certificate validation." };

        var command = new Command("check", "Check multiple endpoints in one run.");
        command.Add(urlsArgument);
        command.Add(configOption);
        command.Add(failFastOption);
        command.Add(fullRunOption);
        command.Add(attemptsOption);
        command.Add(methodOption);
        command.Add(headersOption);
        command.Add(bodyOption);
        command.Add(jsonOption);
        command.Add(outputOption);
        command.Add(timeoutOption);
        command.Add(insecureOption);

        return new CheckCommandDefinition(command, urlsArgument, configOption, failFastOption, fullRunOption, attemptsOption, methodOption, headersOption, bodyOption, jsonOption, outputOption, timeoutOption, insecureOption);
    }

    private static bool IsHelpToken(string arg)
        => string.Equals(arg, "-h", StringComparison.Ordinal) || string.Equals(arg, "--help", StringComparison.Ordinal);

    private static bool TryParseHeaders(IEnumerable<string> rawHeaders, out IReadOnlyDictionary<string, string> headers, out string? error)
    {
        var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawHeaderGroup in rawHeaders)
        {
            foreach (var header in rawHeaderGroup.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var separatorIndex = header.IndexOf(':');
                if (separatorIndex <= 0)
                {
                    headers = parsed;
                    error = $"Invalid header format: {header}";
                    return false;
                }

                var name = header[..separatorIndex].Trim();
                var value = header[(separatorIndex + 1)..].Trim();
                parsed[name] = value;
            }
        }

        headers = parsed;
        error = null;
        return true;
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

    private static bool IsValidHttpMethod(string value)
        => !string.IsNullOrWhiteSpace(value) && HttpMethodPattern.IsMatch(value);

    [GeneratedRegex("^[!#$%&'*+.^_`|~0-9A-Za-z-]+$")]
    private static partial Regex CreateHttpMethodPattern();

    private sealed record ProbeCommandDefinition(
        RootCommand RootCommand,
        Argument<string?> UrlArgument,
        Option<string?> UrlOption,
        Option<int?> AttemptsOption,
        Option<string?> MethodOption,
        Option<string[]> HeadersOption,
        Option<string?> BodyOption,
        Option<bool> JsonOption,
        Option<string?> OutputOption,
        Option<string?> TimeoutOption,
        Option<bool> InsecureOption);

    private sealed record CheckCommandDefinition(
        Command Command,
        Argument<string[]> UrlsArgument,
        Option<string?> ConfigOption,
        Option<bool> FailFastOption,
        Option<bool> FullRunOption,
        Option<int?> AttemptsOption,
        Option<string?> MethodOption,
        Option<string[]> HeadersOption,
        Option<string?> BodyOption,
        Option<bool> JsonOption,
        Option<string?> OutputOption,
        Option<string?> TimeoutOption,
        Option<bool> InsecureOption);
}
