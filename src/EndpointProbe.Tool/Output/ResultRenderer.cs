using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using A2G.EndpointProbe.Tool.Models;

namespace A2G.EndpointProbe.Tool.Output;

public sealed class ResultRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string Render(ProbeResult result, bool asJson)
        => asJson ? JsonSerializer.Serialize(result, JsonOptions) : RenderConsole(result);

    private static string RenderConsole(ProbeResult result)
    {
        var builder = new StringBuilder();
        AppendHeader(builder, "Endpoint");
        builder.AppendLine($"URL: {result.Endpoint.Url}");
        builder.AppendLine($"Method: {result.Endpoint.Method}");
        builder.AppendLine($"Attempts: {result.Endpoint.RequestedAttempts}");
        builder.AppendLine($"Timeout: {result.Endpoint.TimeoutSeconds:0.##}s");
        builder.AppendLine($"Insecure TLS: {result.Endpoint.Insecure}");
        if (result.Endpoint.RequestHeaders.Count > 0)
        {
            builder.AppendLine($"Headers: {string.Join(", ", result.Endpoint.RequestHeaders.Select(pair => $"{pair.Key}={pair.Value}"))}");
        }

        AppendHeader(builder, "DNS");
        builder.AppendLine($"Succeeded: {result.Dns.Succeeded}");
        builder.AppendLine($"Lookup: {result.Dns.DurationMs:0.##} ms");
        builder.AppendLine($"Addresses: {(result.Dns.Addresses.Count == 0 ? "<none>" : string.Join(", ", result.Dns.Addresses))}");
        if (!string.IsNullOrWhiteSpace(result.Dns.Error))
        {
            builder.AppendLine($"Error: {result.Dns.Error}");
        }

        AppendHeader(builder, "TLS");
        if (result.Tls is null || result.Tls.Skipped)
        {
            builder.AppendLine("Skipped: non-HTTPS endpoint");
        }
        else
        {
            builder.AppendLine($"Succeeded: {result.Tls.Succeeded}");
            builder.AppendLine($"TCP connect: {result.Tls.TcpConnectMs:0.##} ms");
            builder.AppendLine($"Handshake: {result.Tls.HandshakeMs:0.##} ms");
            builder.AppendLine($"Protocol: {result.Tls.Protocol ?? "<unknown>"}");
            if (result.Tls.Certificate is not null)
            {
                builder.AppendLine($"Certificate: {result.Tls.Certificate.Subject}");
                builder.AppendLine($"Issuer: {result.Tls.Certificate.Issuer}");
                builder.AppendLine($"Valid until: {result.Tls.Certificate.NotAfter:O}");
            }
            if (!string.IsNullOrWhiteSpace(result.Tls.Error))
            {
                builder.AppendLine($"Error: {result.Tls.Error}");
            }
        }

        AppendHeader(builder, "Attempts");
        foreach (var attempt in result.Attempts)
        {
            builder.AppendLine($"#{attempt.AttemptNumber}: {(attempt.Succeeded ? "OK" : "FAIL")} in {attempt.DurationMs:0.##} ms");
            if (attempt.StatusCode is not null)
            {
                builder.AppendLine($"  Status: {attempt.StatusCode} {attempt.ReasonPhrase}");
            }
            if (attempt.Headers.Count > 0)
            {
                builder.AppendLine($"  Headers: {string.Join(", ", attempt.Headers.Select(pair => $"{pair.Key}={pair.Value}"))}");
            }
            if (!string.IsNullOrWhiteSpace(attempt.BodyHash))
            {
                builder.AppendLine($"  Body hash: {attempt.BodyHash}");
            }
            if (!string.IsNullOrWhiteSpace(attempt.BodyPreview))
            {
                builder.AppendLine($"  Body preview: {attempt.BodyPreview}");
            }
            if (!string.IsNullOrWhiteSpace(attempt.Error))
            {
                builder.AppendLine($"  Error: {attempt.Error}");
            }
        }

        AppendHeader(builder, "Summary");
        builder.AppendLine($"Verdict: {result.Summary.Verdict}");
        builder.AppendLine($"Successful attempts: {result.Summary.SuccessfulAttempts}");
        builder.AppendLine($"Failed attempts: {result.Summary.FailedAttempts}");
        builder.AppendLine($"Stable: {result.Summary.IsStable}");
        builder.AppendLine($"Exit code: {(int)result.Summary.ExitCode}");
        if (!string.IsNullOrWhiteSpace(result.Summary.Notes))
        {
            builder.AppendLine($"Notes: {result.Summary.Notes}");
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendHeader(StringBuilder builder, string title)
    {
        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.AppendLine(title);
        builder.AppendLine(new string('-', title.Length));
    }
}

