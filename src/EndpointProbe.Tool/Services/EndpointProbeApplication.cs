using A2G.EndpointProbe.Tool.Cli;
using A2G.EndpointProbe.Tool.Models;
using A2G.EndpointProbe.Tool.Output;
using Microsoft.Extensions.Logging;

namespace A2G.EndpointProbe.Tool.Services;

public sealed class EndpointProbeApplication(
    IEndpointProbeService endpointProbeService,
    CheckExecutor checkExecutor,
    ResultRenderer resultRenderer,
    ILogger<EndpointProbeApplication> logger)
{
    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        var parseResult = CliParser.Parse(args);
        if (parseResult.ShowHelp)
        {
            Console.WriteLine(CliParser.HelpText);
            return (int)ExitCode.Success;
        }

        if (!parseResult.IsSuccess || parseResult.Mode is null)
        {
            Console.Error.WriteLine(parseResult.Error ?? "Invalid usage.");
            Console.Error.WriteLine();
            Console.Error.WriteLine(CliParser.HelpText);
            return (int)ExitCode.InvalidUsage;
        }

        try
        {
            return parseResult.Mode switch
            {
                ExecutionMode.Probe when parseResult.ProbeOptions is not null => await RunProbeAsync(parseResult.ProbeOptions, cancellationToken),
                ExecutionMode.Check when parseResult.CheckOptions is not null => await RunCheckAsync(parseResult.CheckOptions, cancellationToken),
                _ => (int)ExitCode.InvalidUsage
            };
        }
        catch (CheckConfigurationException ex)
        {
            Console.Error.WriteLine($"Invalid config: {ex.Message}");
            return (int)ExitCode.InvalidUsage;
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Probe cancelled");
            return (int)ExitCode.Cancelled;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Output failure: {ex.Message}");
            return (int)ExitCode.OutputFailure;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Probe failed unexpectedly");
            Console.Error.WriteLine($"Internal error: {ex.Message}");
            return (int)ExitCode.InternalError;
        }
    }

    private async Task<int> RunProbeAsync(CliOptions options, CancellationToken cancellationToken)
    {
        var result = await endpointProbeService.ProbeAsync(options, cancellationToken);
        var rendered = resultRenderer.Render(result, options.Json);
        Console.WriteLine(rendered);

        if (!string.IsNullOrWhiteSpace(options.Output))
        {
            await File.WriteAllTextAsync(options.Output!, rendered + Environment.NewLine, cancellationToken);
        }

        return result.Summary.ExitCode switch
        {
            ExitCodeValue.Success => (int)ExitCode.Success,
            ExitCodeValue.DnsFailure => (int)ExitCode.DnsFailure,
            ExitCodeValue.TlsFailure => (int)ExitCode.TlsFailure,
            ExitCodeValue.HttpFailure => (int)ExitCode.HttpFailure,
            ExitCodeValue.Unstable => (int)ExitCode.Unstable,
            _ => (int)ExitCode.InternalError
        };
    }

    private async Task<int> RunCheckAsync(CheckCliOptions options, CancellationToken cancellationToken)
    {
        var report = await checkExecutor.ExecuteAsync(options, cancellationToken);
        var rendered = resultRenderer.Render(report, options.Json);
        Console.WriteLine(rendered);

        if (!string.IsNullOrWhiteSpace(options.Output))
        {
            await File.WriteAllTextAsync(options.Output!, rendered + Environment.NewLine, cancellationToken);
        }

        return report.Summary.ExitCode switch
        {
            ExitCodeValue.Success => (int)ExitCode.Success,
            ExitCodeValue.Unstable => (int)ExitCode.Unstable,
            _ => (int)ExitCode.InternalError
        };
    }
}
