using YamlDotNet.Serialization;

namespace Marisa.Configuration;

public class WebConfiguration
{
    public string? Private { get; set; }

    public string? Public { get; set; }

    [YamlIgnore]
    public string PrivateBaseUrl => BuildUrl(Private, "web.private");

    [YamlIgnore]
    public string PublicBaseUrl => BuildUrl(Public, "web.public");

    private static string BuildUrl(string? endpoint, string configKey)
    {
        var value = ConfigurationManager.RequireString(configKey, endpoint);
        var trimmed = value.Trim();

        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (!trimmed.Contains(':'))
        {
            throw new MissingConfigurationException($"{configKey}: port is required");
        }

        return $"http://{trimmed}";
    }
}