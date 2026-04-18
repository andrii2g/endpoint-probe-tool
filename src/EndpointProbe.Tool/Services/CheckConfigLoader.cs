using System.Text.Json;
using System.Text.RegularExpressions;
using A2G.EndpointProbe.Tool.Models;

namespace A2G.EndpointProbe.Tool.Services;

public sealed partial class CheckConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex HttpMethodPattern = CreateHttpMethodPattern();

    public async Task<CheckConfig> LoadAsync(string path, CancellationToken cancellationToken)
    {
        string json;
        try
        {
            json = await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new CheckConfigurationException($"Unable to read check config '{path}': {ex.Message}", ex);
        }

        RawEndpointCheckDefinition[] rawDefinitions;
        try
        {
            rawDefinitions = JsonSerializer.Deserialize<RawEndpointCheckDefinition[]>(json, JsonOptions)
                ?? throw new CheckConfigurationException("Check config must contain a JSON array.");
        }
        catch (JsonException ex)
        {
            throw new CheckConfigurationException($"Invalid check config JSON: {ex.Message}", ex);
        }

        if (rawDefinitions.Length == 0)
        {
            throw new CheckConfigurationException("Check config must contain at least one endpoint definition.");
        }

        var definitions = new List<EndpointCheckDefinition>(rawDefinitions.Length);
        for (var index = 0; index < rawDefinitions.Length; index++)
        {
            definitions.Add(NormalizeDefinition(rawDefinitions[index], index));
        }

        return new CheckConfig(definitions);
    }

    private static EndpointCheckDefinition NormalizeDefinition(RawEndpointCheckDefinition raw, int index)
    {
        var label = $"Endpoint #{index + 1}";
        if (string.IsNullOrWhiteSpace(raw.Url))
        {
            throw new CheckConfigurationException($"{label}: 'url' is required.");
        }

        if (!Uri.TryCreate(raw.Url, UriKind.Absolute, out var url) || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
        {
            throw new CheckConfigurationException($"{label}: invalid URL '{raw.Url}'.");
        }

        var method = string.IsNullOrWhiteSpace(raw.Method) ? "GET" : raw.Method.Trim().ToUpperInvariant();
        if (!HttpMethodPattern.IsMatch(method))
        {
            throw new CheckConfigurationException($"{label}: invalid HTTP method '{raw.Method}'.");
        }

        if (raw.ExpectedStatus is null || raw.ExpectedStatus < 100 || raw.ExpectedStatus > 599)
        {
            throw new CheckConfigurationException($"{label}: expectedStatus must be between 100 and 599.");
        }

        var name = string.IsNullOrWhiteSpace(raw.Name)
            ? EndpointCheckDefinition.CreateDefaultName(url)
            : raw.Name.Trim();

        return new EndpointCheckDefinition(name, url, method, raw.ExpectedStatus.Value);
    }

    [GeneratedRegex("^[!#$%&'*+.^_`|~0-9A-Za-z-]+$")]
    private static partial Regex CreateHttpMethodPattern();

    private sealed record RawEndpointCheckDefinition(string? Name, string? Url, string? Method, int? ExpectedStatus);
}
