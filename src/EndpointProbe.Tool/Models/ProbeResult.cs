namespace A2G.EndpointProbe.Tool.Models;

public sealed record ProbeResult(
    EndpointInfo Endpoint,
    DnsProbeResult Dns,
    TlsProbeResult? Tls,
    IReadOnlyList<HttpAttemptResult> Attempts,
    ProbeSummary Summary);

public sealed record EndpointInfo(
    string Url,
    string Host,
    string Scheme,
    int Port,
    string Method,
    int RequestedAttempts,
    bool Insecure,
    double TimeoutSeconds,
    IReadOnlyDictionary<string, string> RequestHeaders);

public sealed record DnsProbeResult(
    bool Succeeded,
    double DurationMs,
    IReadOnlyList<string> Addresses,
    string? Error);

public sealed record TlsProbeResult(
    bool Skipped,
    bool Succeeded,
    double TcpConnectMs,
    double HandshakeMs,
    string? Protocol,
    CertificateInfo? Certificate,
    string? Error);

public sealed record CertificateInfo(
    string? Subject,
    string? Issuer,
    DateTimeOffset? NotBefore,
    DateTimeOffset? NotAfter,
    string? Thumbprint,
    string? SerialNumber);

public sealed record HttpAttemptResult(
    int AttemptNumber,
    bool Succeeded,
    double DurationMs,
    int? StatusCode,
    string? ReasonPhrase,
    IReadOnlyDictionary<string, string> Headers,
    string? BodyPreview,
    string? BodyHash,
    string? Error);

public enum ExitCodeValue
{
    Success = 0,
    InvalidUsage = 1,
    DnsFailure = 2,
    TlsFailure = 3,
    HttpFailure = 4,
    Unstable = 5,
    OutputFailure = 6,
    Cancelled = 7,
    InternalError = 10
}

public sealed record ProbeSummary(
    int SuccessfulAttempts,
    int FailedAttempts,
    bool IsStable,
    string Verdict,
    string? Notes,
    ExitCodeValue ExitCode);

