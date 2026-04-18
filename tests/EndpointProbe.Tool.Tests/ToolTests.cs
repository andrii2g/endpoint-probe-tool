using System.Xml.Linq;
using A2G.EndpointProbe.Tool.Cli;
using A2G.EndpointProbe.Tool.Models;
using A2G.EndpointProbe.Tool.Output;
using A2G.EndpointProbe.Tool.Services;
using Xunit;

namespace A2G.EndpointProbe.Tool.Tests;

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
        Assert.Equal(ExecutionMode.Probe, result.Mode);
        Assert.NotNull(result.ProbeOptions);
        Assert.Equal(new Uri("https://example.com"), result.ProbeOptions!.Url);
        Assert.Equal(3, result.ProbeOptions.Attempts);
        Assert.Equal("POST", result.ProbeOptions.Method.Method);
        Assert.Equal("one", result.ProbeOptions.Headers["X-Test"]);
        Assert.Equal("application/json", result.ProbeOptions.Headers["Accept"]);
        Assert.True(result.ProbeOptions.Json);
        Assert.True(result.ProbeOptions.Insecure);
        Assert.Equal(TimeSpan.FromSeconds(20), result.ProbeOptions.Timeout);
    }

    [Fact]
    public void Defaults_Body_Request_To_Post_When_Method_Not_Supplied()
    {
        var result = CliParser.Parse(["https://example.com", "--body", "payload"]);

        Assert.True(result.IsSuccess);
        Assert.Equal("POST", result.ProbeOptions!.Method.Method);
    }

    [Fact]
    public void Parses_Check_Config_Mode()
    {
        var result = CliParser.Parse(["check", "--config", "endpoints.json", "--attempts", "2", "--json", "--timeout", "30", "--insecure"]);

        Assert.True(result.IsSuccess);
        Assert.Equal(ExecutionMode.Check, result.Mode);
        Assert.NotNull(result.CheckOptions);
        Assert.Equal("endpoints.json", result.CheckOptions!.ConfigPath);
        Assert.Empty(result.CheckOptions.Endpoints);
        Assert.True(result.CheckOptions.FullRun);
        Assert.Equal(2, result.CheckOptions.Attempts);
        Assert.True(result.CheckOptions.Json);
        Assert.True(result.CheckOptions.Insecure);
        Assert.Equal(TimeSpan.FromSeconds(30), result.CheckOptions.Timeout);
    }

    [Fact]
    public void Parses_Check_MultipleUrls_And_Defaults_To_FullRun()
    {
        var result = CliParser.Parse(["check", "https://api.example.com/health", "https://auth.example.com/health"]);

        Assert.True(result.IsSuccess);
        Assert.Equal(ExecutionMode.Check, result.Mode);
        Assert.NotNull(result.CheckOptions);
        Assert.True(result.CheckOptions!.FullRun);
        Assert.Equal(2, result.CheckOptions.Endpoints.Count);
        Assert.Equal("GET", result.CheckOptions.Endpoints[0].Method);
        Assert.Equal(200, result.CheckOptions.Endpoints[0].ExpectedStatus);
    }

    [Fact]
    public void Rejects_InvalidHeader()
    {
        var result = CliParser.Parse(["https://example.com", "--headers", "broken"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid header format: broken", result.Error);
    }

    [Fact]
    public void Rejects_DuplicateUrlSources()
    {
        var result = CliParser.Parse(["https://example.com", "--url", "https://contoso.com"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Specify the URL either positionally or with --url, not both.", result.Error);
    }

    [Fact]
    public void Rejects_Check_Flag_Conflict()
    {
        var result = CliParser.Parse(["check", "https://example.com", "--fail-fast", "--full-run"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("Specify either --fail-fast or --full-run, not both.", result.Error);
    }

    [Fact]
    public void Rejects_Request_Shaping_Options_In_Check_Mode()
    {
        var result = CliParser.Parse(["check", "https://example.com", "--method", "POST"]);

        Assert.False(result.IsSuccess);
        Assert.Equal("--method, --headers, and --body are not supported in check mode. Use a config file to define per-endpoint request details.", result.Error);
    }
}

public sealed class CheckConfigLoaderTests
{
    [Fact]
    public async Task Loads_Valid_Array_Config()
    {
        var filePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(filePath, """
[
  {
    "name": "api",
    "url": "https://api.example.com/health",
    "method": "GET",
    "expectedStatus": 200
  }
]
""", TestContext.Current.CancellationToken);

        try
        {
            var loader = new CheckConfigLoader();
            var config = await loader.LoadAsync(filePath, TestContext.Current.CancellationToken);

            Assert.Single(config.Endpoints);
            Assert.Equal("api", config.Endpoints[0].Name);
            Assert.Equal(200, config.Endpoints[0].ExpectedStatus);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task Invalid_Json_Fails_Clearly()
    {
        var filePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(filePath, "[", TestContext.Current.CancellationToken);

        try
        {
            var loader = new CheckConfigLoader();
            var exception = await Assert.ThrowsAsync<CheckConfigurationException>(() => loader.LoadAsync(filePath, TestContext.Current.CancellationToken));

            Assert.StartsWith("Invalid check config JSON:", exception.Message);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task Missing_Url_Fails_Clearly()
    {
        var filePath = Path.GetTempFileName();
        await File.WriteAllTextAsync(filePath, """
[
  {
    "name": "api",
    "method": "GET",
    "expectedStatus": 200
  }
]
""", TestContext.Current.CancellationToken);

        try
        {
            var loader = new CheckConfigLoader();
            var exception = await Assert.ThrowsAsync<CheckConfigurationException>(() => loader.LoadAsync(filePath, TestContext.Current.CancellationToken));

            Assert.Equal("Endpoint #1: 'url' is required.", exception.Message);
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}

public sealed class CheckExecutorTests
{
    [Fact]
    public async Task Expected_Status_Match_Passes()
    {
        var service = new FakeEndpointProbeService([TestProbes.CreateSuccessfulProbeResult("https://api.example.com/health", 200)]);
        var executor = new CheckExecutor(service, new CheckConfigLoader());
        var options = new CheckCliOptions(null, [new EndpointCheckDefinition("api", new Uri("https://api.example.com/health"), "GET", 200)], 1, false, null, TimeSpan.FromSeconds(15), false, false);

        var report = await executor.ExecuteAsync(options, TestContext.Current.CancellationToken);

        Assert.True(report.Results[0].Passed);
        Assert.Equal(ExitCodeValue.Success, report.Summary.ExitCode);
    }

    [Fact]
    public async Task Expected_Status_Mismatch_Fails_And_Sets_Aggregate_Exit_Code()
    {
        var service = new FakeEndpointProbeService([TestProbes.CreateSuccessfulProbeResult("https://auth.example.com/login", 500)]);
        var executor = new CheckExecutor(service, new CheckConfigLoader());
        var options = new CheckCliOptions(null, [new EndpointCheckDefinition("auth", new Uri("https://auth.example.com/login"), "GET", 302)], 1, false, null, TimeSpan.FromSeconds(15), false, false);

        var report = await executor.ExecuteAsync(options, TestContext.Current.CancellationToken);

        Assert.False(report.Results[0].Passed);
        Assert.Equal("expected 302, got 500", report.Results[0].FailureReason);
        Assert.Equal(ExitCodeValue.Unstable, report.Summary.ExitCode);
    }

    [Fact]
    public async Task Fail_Fast_Stops_After_First_Failure()
    {
        var service = new FakeEndpointProbeService([
            TestProbes.CreateSuccessfulProbeResult("https://api.example.com/health", 500),
            TestProbes.CreateSuccessfulProbeResult("https://auth.example.com/health", 200)
        ]);
        var executor = new CheckExecutor(service, new CheckConfigLoader());
        var options = new CheckCliOptions(null,
        [
            new EndpointCheckDefinition("api", new Uri("https://api.example.com/health"), "GET", 200),
            new EndpointCheckDefinition("auth", new Uri("https://auth.example.com/health"), "GET", 200)
        ], 1, false, null, TimeSpan.FromSeconds(15), false, true);

        var report = await executor.ExecuteAsync(options, TestContext.Current.CancellationToken);

        Assert.Single(report.Results);
        Assert.Equal(1, service.CallCount);
    }

    [Fact]
    public async Task Full_Run_Continues_After_Failure_And_Aggregates_Counts()
    {
        var service = new FakeEndpointProbeService([
            TestProbes.CreateSuccessfulProbeResult("https://api.example.com/health", 500),
            TestProbes.CreateSuccessfulProbeResult("https://auth.example.com/health", 200)
        ]);
        var executor = new CheckExecutor(service, new CheckConfigLoader());
        var options = new CheckCliOptions(null,
        [
            new EndpointCheckDefinition("api", new Uri("https://api.example.com/health"), "GET", 200),
            new EndpointCheckDefinition("auth", new Uri("https://auth.example.com/health"), "GET", 200)
        ], 1, false, null, TimeSpan.FromSeconds(15), false, false);

        var report = await executor.ExecuteAsync(options, TestContext.Current.CancellationToken);

        Assert.Equal(2, report.Results.Count);
        Assert.Equal(1, report.Summary.PassedEndpoints);
        Assert.Equal(1, report.Summary.FailedEndpoints);
        Assert.Equal(["api"], report.Summary.FailedEndpointNames);
        Assert.Equal(2, service.CallCount);
    }
}

public sealed class VerdictEngineTests
{
    [Fact]
    public void Returns_Unstable_For_DifferentSuccessfulFingerprints()
    {
        var summary = VerdictEngine.CreateSummary(
            new DnsProbeResult(true, 1, ["127.0.0.1"], null),
            new TlsProbeResult(false, true, 1, 1, "Tls13", null, null, null),
            [
                new HttpAttemptResult(1, true, 10, 200, "OK", new Dictionary<string, string> { ["server"] = "a" }, "alpha", "hash-a", null, false, null),
                new HttpAttemptResult(2, true, 10, 200, "OK", new Dictionary<string, string> { ["server"] = "b" }, "beta", "hash-b", null, false, null)
            ]);

        Assert.Equal(ExitCodeValue.Unstable, summary.ExitCode);
        Assert.False(summary.IsStable);
    }
}

public sealed class ResultRendererTests
{
    [Fact]
    public void Renders_Json_WithCamelCase_AndStableSections()
    {
        var renderer = new ResultRenderer();
        var result = CreateProbeResult();

        var json = renderer.Render(result, asJson: true);

        Assert.Contains("\"endpoint\"", json);
        Assert.Contains("\"successfulAttempts\"", json);
        Assert.Contains("\"exitCode\": \"Success\"", json);
        Assert.Contains("\"tls\"", json);
        Assert.Contains("\"failureKind\": \"certificate_validation\"", json);
        Assert.Contains("\"isRedirect\": true", json);
    }

    [Fact]
    public void Renders_Console_Redirect_And_Tls_Failure_Metadata()
    {
        var renderer = new ResultRenderer();
        var output = renderer.Render(CreateProbeResult(), asJson: false);

        Assert.Contains("Failure kind: certificate_validation", output);
        Assert.Contains("Redirect: https://origin.example.com/login", output);
        Assert.Contains("Body hash: abc", output);
    }

    [Fact]
    public void Renders_Check_Report_With_Aggregate_Exit_Code()
    {
        var renderer = new ResultRenderer();
        var report = new CheckRunReport(
            ExecutionMode.Check,
            FailFast: false,
            FullRun: true,
            ConfiguredEndpoints: 2,
            [
                new EndpointCheckResult("api", new EndpointCheckDefinition("api", new Uri("https://api.example.com/health"), "GET", 200), true, 200, 120, null, TestProbes.CreateSuccessfulProbeResult("https://api.example.com/health", 200)),
                new EndpointCheckResult("auth", new EndpointCheckDefinition("auth", new Uri("https://auth.example.com/login"), "GET", 302), false, 500, 90, "expected 302, got 500", TestProbes.CreateSuccessfulProbeResult("https://auth.example.com/login", 500))
            ],
            new CheckRunSummary(2, 1, 1, 210, ["auth"], false, ExitCodeValue.Unstable));

        var json = renderer.Render(report, asJson: true);
        var console = renderer.Render(report, asJson: false);

        Assert.Contains("\"configuredEndpoints\": 2", json);
        Assert.Contains("\"exitCode\": \"Unstable\"", json);
        Assert.Contains("[1/2] api", console);
        Assert.Contains("- exitCode: 5", console);
    }

    [Fact]
    public void Package_Project_IsConfigured_AsDotnetTool()
    {
        var projectPath = FindProjectFile();
        var document = XDocument.Load(projectPath);

        Assert.Equal("true", document.Descendants("PackAsTool").Single().Value);
        Assert.Equal("endpoint-probe", document.Descendants("ToolCommandName").Single().Value);
        Assert.Equal("EndpointProbe.Tool", document.Descendants("PackageId").Single().Value);
    }

    private static string FindProjectFile()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "EndpointProbe.Tool", "EndpointProbe.Tool.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate src/EndpointProbe.Tool/EndpointProbe.Tool.csproj from the test output directory.");
    }

    private static ProbeResult CreateProbeResult()
        => new(
            new EndpointInfo("https://example.com", "example.com", "https", 443, "GET", 1, false, 15, new Dictionary<string, string>()),
            new DnsProbeResult(true, 4.2, ["93.184.216.34"], null),
            new TlsProbeResult(false, false, 10, 20, "Tls13", new CertificateInfo("CN=example.com", "CN=issuer", DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30), "thumb", "serial"), "Certificate validation failed: RemoteCertificateNameMismatch", "certificate_validation"),
            [new HttpAttemptResult(1, true, 40, 302, "Found", new Dictionary<string, string> { ["server"] = "example", ["location"] = "https://origin.example.com/login" }, "hello", "abc", null, true, "https://origin.example.com/login")],
            new ProbeSummary(1, 0, true, "Stable", null, ExitCodeValue.Success));
}

file sealed class FakeEndpointProbeService(IReadOnlyList<ProbeResult> results) : IEndpointProbeService
{
    private int index;

    public int CallCount { get; private set; }

    public Task<ProbeResult> ProbeAsync(CliOptions options, CancellationToken cancellationToken)
    {
        CallCount++;
        var result = results[Math.Min(index, results.Count - 1)];
        index++;
        return Task.FromResult(result);
    }
}

file static class TestProbes
{
    public static ProbeResult CreateSuccessfulProbeResult(string url, int statusCode)
        => new(
            new EndpointInfo(url, new Uri(url).DnsSafeHost, new Uri(url).Scheme, new Uri(url).Port, "GET", 1, false, 15, new Dictionary<string, string>()),
            new DnsProbeResult(true, 1, ["127.0.0.1"], null),
            new TlsProbeResult(false, true, 1, 1, "Tls13", null, null, null),
            [new HttpAttemptResult(1, true, 10, statusCode, "OK", new Dictionary<string, string>(), null, null, null, false, null)],
            new ProbeSummary(1, 0, true, "Stable", null, ExitCodeValue.Success));
}
