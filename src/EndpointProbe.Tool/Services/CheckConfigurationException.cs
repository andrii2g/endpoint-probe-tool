namespace A2G.EndpointProbe.Tool.Services;

public sealed class CheckConfigurationException(string message, Exception? innerException = null) : Exception(message, innerException)
{
}
