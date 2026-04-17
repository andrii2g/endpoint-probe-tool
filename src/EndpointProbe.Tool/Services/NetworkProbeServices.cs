using System.Diagnostics;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using A2G.EndpointProbe.Tool.Cli;
using A2G.EndpointProbe.Tool.Models;

namespace A2G.EndpointProbe.Tool.Services;

public interface IDnsProbeService
{
    Task<DnsProbeResult> ProbeAsync(Uri url, CancellationToken cancellationToken);
}

public interface ITlsProbeService
{
    Task<TlsProbeResult> ProbeAsync(Uri url, bool insecure, TimeSpan timeout, CancellationToken cancellationToken);
}

public static class HttpClientNames
{
    public const string Secure = "probe-secure";
    public const string Insecure = "probe-insecure";
}

public sealed class DnsProbeService : IDnsProbeService
{
    public async Task<DnsProbeResult> ProbeAsync(Uri url, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(url.DnsSafeHost, cancellationToken);
            stopwatch.Stop();
            return new DnsProbeResult(true, stopwatch.Elapsed.TotalMilliseconds, addresses.Select(address => address.ToString()).ToArray(), null);
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            stopwatch.Stop();
            return new DnsProbeResult(false, stopwatch.Elapsed.TotalMilliseconds, Array.Empty<string>(), ex.Message);
        }
    }
}

public sealed class TlsProbeService : ITlsProbeService
{
    public async Task<TlsProbeResult> ProbeAsync(Uri url, bool insecure, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (url.Scheme != Uri.UriSchemeHttps)
        {
            return new TlsProbeResult(true, true, 0, 0, null, null, null, null);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        var tcpConnectMs = 0d;
        var handshakeMs = 0d;
        SslPolicyErrors capturedPolicyErrors = SslPolicyErrors.None;

        try
        {
            using var tcpClient = new TcpClient();
            var connectStopwatch = Stopwatch.StartNew();
            await tcpClient.ConnectAsync(url.DnsSafeHost, url.Port, timeoutCts.Token);
            connectStopwatch.Stop();
            tcpConnectMs = connectStopwatch.Elapsed.TotalMilliseconds;

            using var networkStream = tcpClient.GetStream();
            using var sslStream = new SslStream(networkStream, false, (_, _, _, errors) =>
            {
                capturedPolicyErrors = errors;
                return true;
            });

            var handshakeStopwatch = Stopwatch.StartNew();
            await sslStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = url.DnsSafeHost,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck
            }, timeoutCts.Token);
            handshakeStopwatch.Stop();
            handshakeMs = handshakeStopwatch.Elapsed.TotalMilliseconds;

            var remoteCertificate = sslStream.RemoteCertificate is null ? null : new X509Certificate2(sslStream.RemoteCertificate);
            var certificate = remoteCertificate is null
                ? null
                : new CertificateInfo(
                    remoteCertificate.Subject,
                    remoteCertificate.Issuer,
                    remoteCertificate.NotBefore,
                    remoteCertificate.NotAfter,
                    remoteCertificate.Thumbprint,
                    remoteCertificate.SerialNumber);

            if (!insecure && capturedPolicyErrors != SslPolicyErrors.None)
            {
                return new TlsProbeResult(
                    false,
                    false,
                    tcpConnectMs,
                    handshakeMs,
                    sslStream.SslProtocol.ToString(),
                    certificate,
                    $"Certificate validation failed: {capturedPolicyErrors}",
                    "certificate_validation");
            }

            return new TlsProbeResult(false, true, tcpConnectMs, handshakeMs, sslStream.SslProtocol.ToString(), certificate, null, null);
        }
        catch (OperationCanceledException)
        {
            var transportError = tcpConnectMs > 0
                ? $"TLS handshake timed out after {timeout.TotalSeconds:0.##} seconds."
                : $"TCP connect timed out after {timeout.TotalSeconds:0.##} seconds.";
            var failureKind = tcpConnectMs > 0 ? "handshake_timeout" : "connect_timeout";
            return new TlsProbeResult(false, false, tcpConnectMs, handshakeMs, null, null, transportError, failureKind);
        }
        catch (AuthenticationException ex)
        {
            return new TlsProbeResult(false, false, tcpConnectMs, handshakeMs, null, null, ex.Message, "protocol_negotiation");
        }
        catch (SocketException ex)
        {
            return new TlsProbeResult(false, false, tcpConnectMs, handshakeMs, null, null, ex.Message, "transport");
        }
        catch (IOException ex)
        {
            return new TlsProbeResult(false, false, tcpConnectMs, handshakeMs, null, null, ex.Message, "transport");
        }
    }
}
