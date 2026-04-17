using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using A2G.EndpointProbe.Tool.Cli;
using A2G.EndpointProbe.Tool.Models;
using Microsoft.Extensions.Logging;

namespace A2G.EndpointProbe.Tool.Services;

public interface IEndpointProbeService
{
    Task<ProbeResult> ProbeAsync(CliOptions options, CancellationToken cancellationToken);
}

public sealed class EndpointProbeService(
    IDnsProbeService dnsProbeService,
    ITlsProbeService tlsProbeService,
    IHttpClientFactory httpClientFactory,
    ILogger<EndpointProbeService> logger) : IEndpointProbeService
{
    private static readonly string[] FingerprintHeaders = ["server", "via", "cf-cache-status", "cf-ray", "x-powered-by", "location", "content-type", "content-length"];

    public async Task<ProbeResult> ProbeAsync(CliOptions options, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.Timeout);

        logger.LogInformation("Probing {Url} with {Attempts} attempt(s)", options.Url, options.Attempts);

        var dns = await dnsProbeService.ProbeAsync(options.Url, timeoutCts.Token);
        var tls = await tlsProbeService.ProbeAsync(options.Url, options.Insecure, options.Timeout, timeoutCts.Token);
        var attempts = new List<HttpAttemptResult>(options.Attempts);
        for (var attemptNumber = 1; attemptNumber <= options.Attempts; attemptNumber++)
        {
            attempts.Add(await ExecuteAttemptAsync(options, attemptNumber, timeoutCts.Token));
        }

        var summary = VerdictEngine.CreateSummary(dns, tls, attempts);
        return new ProbeResult(
            new EndpointInfo(
                options.Url.ToString(),
                options.Url.DnsSafeHost,
                options.Url.Scheme,
                options.Url.Port,
                options.Method.Method,
                options.Attempts,
                options.Insecure,
                options.Timeout.TotalSeconds,
                new Dictionary<string, string>(options.Headers, StringComparer.OrdinalIgnoreCase)),
            dns,
            tls,
            attempts,
            summary);
    }

    private async Task<HttpAttemptResult> ExecuteAttemptAsync(CliOptions options, int attemptNumber, CancellationToken cancellationToken)
    {
        var clientName = options.Insecure ? HttpClientNames.Insecure : HttpClientNames.Secure;
        var client = httpClientFactory.CreateClient(clientName);
        using var request = new HttpRequestMessage(options.Method, options.Url);
        foreach (var header in options.Headers)
        {
            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                request.Content ??= new StringContent(string.Empty);
                request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        if (options.Body is not null)
        {
            request.Content = new StringContent(options.Body, Encoding.UTF8, "application/json");
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var bodyPreview = await ReadPreviewAsync(response, cancellationToken);
            stopwatch.Stop();

            var headers = ExtractHeaders(response);
            var redirectLocation = headers.TryGetValue("location", out var location) ? location : null;
            var isRedirect = (int)response.StatusCode is >= 300 and < 400 && !string.IsNullOrWhiteSpace(redirectLocation);

            return new HttpAttemptResult(
                attemptNumber,
                true,
                stopwatch.Elapsed.TotalMilliseconds,
                (int)response.StatusCode,
                response.ReasonPhrase,
                headers,
                bodyPreview,
                bodyPreview is null ? null : ComputeHash(bodyPreview),
                null,
                isRedirect,
                redirectLocation);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            stopwatch.Stop();
            return new HttpAttemptResult(
                attemptNumber,
                false,
                stopwatch.Elapsed.TotalMilliseconds,
                null,
                null,
                new Dictionary<string, string>(),
                null,
                null,
                ex.Message,
                false,
                null);
        }
    }

    private static IReadOnlyDictionary<string, string> ExtractHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in FingerprintHeaders)
        {
            if (response.Headers.TryGetValues(name, out var values) || response.Content.Headers.TryGetValues(name, out values))
            {
                headers[name] = string.Join(", ", values);
            }
        }

        return headers;
    }

    private static async Task<string?> ReadPreviewAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength is 0)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[1024];
        var remaining = 4096;
        while (remaining > 0)
        {
            var read = await stream.ReadAsync(chunk.AsMemory(0, Math.Min(chunk.Length, remaining)), cancellationToken);
            if (read == 0)
            {
                break;
            }

            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            remaining -= read;
        }

        if (buffer.Length == 0)
        {
            return null;
        }

        var raw = Encoding.UTF8.GetString(buffer.ToArray());
        return raw.Length <= 400 ? raw : string.Concat(raw.AsSpan(0, 400), "...");
    }

    private static string ComputeHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public static class VerdictEngine
{
    public static ProbeSummary CreateSummary(DnsProbeResult dns, TlsProbeResult? tls, IReadOnlyList<HttpAttemptResult> attempts)
    {
        if (!dns.Succeeded)
        {
            return new ProbeSummary(0, attempts.Count, false, "DNS failure", dns.Error, ExitCodeValue.DnsFailure);
        }

        if (tls is { Skipped: false, Succeeded: false })
        {
            return new ProbeSummary(0, attempts.Count, false, "TLS failure", tls.Error, ExitCodeValue.TlsFailure);
        }

        var successfulAttempts = attempts.Count(attempt => attempt.Succeeded);
        var failedAttempts = attempts.Count - successfulAttempts;
        if (successfulAttempts == 0)
        {
            var note = attempts.Select(attempt => attempt.Error).FirstOrDefault(error => !string.IsNullOrWhiteSpace(error));
            return new ProbeSummary(0, failedAttempts, false, "HTTP failure", note, ExitCodeValue.HttpFailure);
        }

        var stable = IsStable(attempts.Where(attempt => attempt.Succeeded).ToArray());
        return stable
            ? new ProbeSummary(successfulAttempts, failedAttempts, true, "Stable", failedAttempts > 0 ? "Some attempts failed after retries." : null, ExitCodeValue.Success)
            : new ProbeSummary(successfulAttempts, failedAttempts, false, "Unstable", "Successful attempts returned different fingerprints.", ExitCodeValue.Unstable);
    }

    private static bool IsStable(IReadOnlyList<HttpAttemptResult> attempts)
    {
        if (attempts.Count <= 1)
        {
            return true;
        }

        var first = attempts[0];
        return attempts.All(attempt =>
            attempt.StatusCode == first.StatusCode &&
            string.Equals(attempt.BodyHash, first.BodyHash, StringComparison.OrdinalIgnoreCase) &&
            attempt.IsRedirect == first.IsRedirect &&
            string.Equals(attempt.RedirectLocation, first.RedirectLocation, StringComparison.Ordinal) &&
            HeadersMatch(attempt.Headers, first.Headers));
    }

    private static bool HeadersMatch(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var other) || !string.Equals(pair.Value, other, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
