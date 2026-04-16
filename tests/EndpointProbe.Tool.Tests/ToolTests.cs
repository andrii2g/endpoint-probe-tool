using System.Xml.Linq;
using A2G.EndpointProbe.Tool.Cli;
using A2G.EndpointProbe.Tool.Models;
using A2G.EndpointProbe.Tool.Output;
using A2G.EndpointProbe.Tool.Services;
using Xunit;

namespace A2G.A2G.EndpointProbe.Tool.Tests;

public sealed class CliParserTests
{
    [Fact]
    public void Parses_PositionalUrl_AndOptions()
    {
        var result = CliParser.Parse([
            "https://example.com",
            "--attempts", "3",
            "--method", "post",
            "--headers", "X-Test: one;Accept: application/json",
            "--body", "{\"ok\":true}",
            "--json",
            "--timeout", "20",
            "--insecure"
        ]);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Options);
        Assert.Equal(new Uri("https://example.com"), result.Options!.Url);
        Assert.Equal(3, result.Options.Attempts);
        Assert.Equal("POST", result.Options.Method.Method);
        Assert.Equal("one", result.Options.Headers["X-Test"]);
        Assert.Equal("application/json", result.Options.Headers["Accept"]);
        Assert.True(result.Options.Json);
        Assert.True(result.Options.Insecure);
        Assert.Equal(TimeSpan.FromSeconds(20), result.Options.Timeout);
    }

    [Fact]
    public void Rejects_InvalidHeader()
    {
        var result = CliParser.Parse(["https://example.com", "--headers", "broken"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid header format: broken", result.Error);
    }
}

public sealed class VerdictEngineTests
{
    [Fact]
    public void Returns_Unstable_For_DifferentSuccessfulFingerprints()
    {
        var summary = VerdictEngine.CreateSummary(
            new DnsProbeResult(true, 1, ["127.0.0.1"], null),
            new TlsProbeResult(false, true, 1, 1, "Tls13", null, null),
            [
                new HttpAttemptResult(1, true, 10, 200, "OK", new Dictionary<string, string> { ["server"] = "a" }, "alpha", "hash-a", null),
                new HttpAttemptResult(2, true, 10, 200, "OK", new Dictionary<string, string> { ["server"] = "b" }, "beta", "hash-b", null)
            ]);

        Assert.Equal(ExitCodeValue.Unstable, summary.ExitCode);
        Assert.False(summary.IsStable);
    }
}

public sealed class ResultRendererTests
{
    [Fact]
    public void Renders_Json_WithCamelCase()
    {
        var renderer = new ResultRenderer();
        var result = CreateProbeResult();

        var json = renderer.Render(result, asJson: true);

        Assert.Contains("\"endpoint\"", json);
        Assert.Contains("\"successfulAttempts\"", json);
        Assert.Contains("\"exitCode\": \"Success\"", json);
    }

    [Fact]
    public void Package_Project_IsConfigured_AsDotnetTool()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "A2G.EndpointProbe.Tool", "A2G.EndpointProbe.Tool.csproj"));
        var document = XDocument.Load(projectPath);

        Assert.Equal("true", document.Descendants("PackAsTool").Single().Value);
        Assert.Equal("endpoint-probe", document.Descendants("ToolCommandName").Single().Value);
        Assert.Equal("A2G.EndpointProbe.Tool", document.Descendants("PackageId").Single().Value);
    }

    private static ProbeResult CreateProbeResult()
        => new(
            new EndpointInfo("https://example.com", "example.com", "https", 443, "GET", 1, false, 15, new Dictionary<string, string>()),
            new DnsProbeResult(true, 4.2, ["93.184.216.34"], null),
            new TlsProbeResult(false, true, 10, 20, "Tls13", new CertificateInfo("CN=example.com", "CN=issuer", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30), "thumb", "serial"), null),
            [new HttpAttemptResult(1, true, 40, 200, "OK", new Dictionary<string, string> { ["server"] = "example" }, "hello", "abc", null)],
            new ProbeSummary(1, 0, true, "Stable", null, ExitCodeValue.Success));
}

