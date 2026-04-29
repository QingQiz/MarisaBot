using YamlDotNet.Serialization;

namespace Marisa.Configuration;

public class WebConfiguration
{
    private const int DefaultPort = 14311;

    public string? Private { get; set; }

    public string? Public { get; set; }

    [YamlIgnore]
    public string PrivateBaseUrl => BuildBaseUrl(Private, DefaultPrivateEndpoint);

    [YamlIgnore]
    public string PublicBaseUrl => BuildBaseUrl(Public, DefaultPublicEndpoint);

    private static string DefaultPrivateEndpoint => $"127.0.0.1:{DefaultPort}";

    private static string DefaultPublicEndpoint => $"localhost:{DefaultPort}";

    private static string BuildBaseUrl(string? endpoint, string fallbackEndpoint)
    {
        var actualEndpoint = NormalizeEndpoint(string.IsNullOrWhiteSpace(endpoint) ? fallbackEndpoint : endpoint);
        return $"http://{actualEndpoint}";
    }

    private static string NormalizeEndpoint(string endpoint)
    {
        var trimmedEndpoint = endpoint.Trim();

        if (trimmedEndpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmedEndpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(trimmedEndpoint, UriKind.Absolute);
            return uri.IsDefaultPort ? $"{uri.Host}:{DefaultPort}" : $"{uri.Host}:{uri.Port}";
        }

        return trimmedEndpoint.Contains(':') ? trimmedEndpoint : $"{trimmedEndpoint}:{DefaultPort}";
    }
}