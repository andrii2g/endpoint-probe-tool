using A2G.EndpointProbe.Tool.Output;
using A2G.EndpointProbe.Tool.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

const string UserAgent = "endpoint-probe/1.2.0";

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss ";
});

builder.Services.AddHttpClient(HttpClientNames.Secure, client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    client.Timeout = Timeout.InfiniteTimeSpan;
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = false,
    AutomaticDecompression = System.Net.DecompressionMethods.All,
    CheckCertificateRevocationList = true
}).AddStandardResilienceHandler(options =>
{
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
    options.Retry.MaxRetryAttempts = 2;
    options.Retry.Delay = TimeSpan.FromMilliseconds(500);
    options.Retry.UseJitter = true;
});

builder.Services.AddHttpClient(HttpClientNames.Insecure, client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    client.Timeout = Timeout.InfiniteTimeSpan;
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AllowAutoRedirect = false,
    AutomaticDecompression = System.Net.DecompressionMethods.All,
    CheckCertificateRevocationList = false,
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
}).AddStandardResilienceHandler(options =>
{
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
    options.Retry.MaxRetryAttempts = 2;
    options.Retry.Delay = TimeSpan.FromMilliseconds(500);
    options.Retry.UseJitter = true;
});

builder.Services.AddSingleton<EndpointProbeApplication>();
builder.Services.AddSingleton<IDnsProbeService, DnsProbeService>();
builder.Services.AddSingleton<ITlsProbeService, TlsProbeService>();
builder.Services.AddSingleton<IEndpointProbeService, EndpointProbeService>();
builder.Services.AddSingleton<CheckConfigLoader>();
builder.Services.AddSingleton<CheckExecutor>();
builder.Services.AddSingleton<ResultRenderer>();

using var host = builder.Build();
var app = host.Services.GetRequiredService<EndpointProbeApplication>();
return await app.RunAsync(args, CancellationToken.None);
