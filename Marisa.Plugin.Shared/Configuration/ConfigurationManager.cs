using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Marisa.Plugin.Shared.Configuration;

public static class ConfigurationManager
{
    private static PluginConfiguration? _config = null;

    public static PluginConfiguration Configuration => _config ??= ReadConfiguration();

    private static PluginConfiguration ReadConfiguration()
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var input = File.ReadAllText(Path.Join(AppDomain.CurrentDomain.BaseDirectory, "config.yaml"));

        return deserializer.Deserialize<PluginConfiguration>(input);
    }
}