using A2G.EndpointProbe.Tool.Cli;
using A2G.EndpointProbe.Tool.Output;
using Microsoft.Extensions.Logging;

namespace A2G.EndpointProbe.Tool.Services;

public sealed class EndpointProbeApplication(
    IEndpointProbeService endpointProbeService,
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

        if (!parseResult.IsSuccess || parseResult.Options is null)
        {
            Console.Error.WriteLine(parseResult.Error ?? "Invalid usage.");
            Console.Error.WriteLine();
            Console.Error.WriteLine(CliParser.HelpText);
            return (int)ExitCode.InvalidUsage;
        }

        try
        {
            var result = await endpointProbeService.ProbeAsync(parseResult.Options, cancellationToken);
            var rendered = resultRenderer.Render(result, parseResult.Options.Json);
            Console.WriteLine(rendered);

            if (!string.IsNullOrWhiteSpace(parseResult.Options.Output))
            {
                await File.WriteAllTextAsync(parseResult.Options.Output!, rendered + Environment.NewLine, cancellationToken);
            }

            return result.Summary.ExitCode switch
            {
                Models.ExitCodeValue.Success => (int)ExitCode.Success,
                Models.ExitCodeValue.DnsFailure => (int)ExitCode.DnsFailure,
                Models.ExitCodeValue.TlsFailure => (int)ExitCode.TlsFailure,
                Models.ExitCodeValue.HttpFailure => (int)ExitCode.HttpFailure,
                Models.ExitCodeValue.Unstable => (int)ExitCode.Unstable,
                _ => (int)ExitCode.InternalError
            };
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
}

