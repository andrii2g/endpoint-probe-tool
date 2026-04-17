namespace A2G.EndpointProbe.Tool;

public enum ExitCode
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

