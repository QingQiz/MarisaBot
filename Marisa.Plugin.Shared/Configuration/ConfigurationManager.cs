using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Marisa.Plugin.Shared.Configuration;

public static class ConfigurationManager
{
    private static PluginConfiguration? _config;

    public static PluginConfiguration Configuration => _config ??= ReadConfiguration();

    private static PluginConfiguration ReadConfiguration()
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var input = File.ReadAllText(Path.Join(AppDomain.CurrentDomain.BaseDirectory, "config.yaml"));

        var config = deserializer.Deserialize<PluginConfiguration>(input);

        if (!Directory.Exists(config.MaiMai.TempPath))
        {
            Directory.CreateDirectory(config.MaiMai.TempPath);
        }
        if (!Directory.Exists(config.Chunithm.TempPath))
        {
            Directory.CreateDirectory(config.Chunithm.TempPath);
        }
        if (!Directory.Exists(config.Osu.TempPath))
        {
            Directory.CreateDirectory(config.Osu.TempPath);
        }
        if (!Directory.Exists(config.Arcaea.TempPath))
        {
            Directory.CreateDirectory(config.Arcaea.TempPath);
        }

        return config;
    }
}