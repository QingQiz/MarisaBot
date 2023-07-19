using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Marisa.Plugin.Shared.Configuration;

public static class ConfigurationManager
{
    private static PluginConfiguration? _config;

    private static string? _configFilePath;

    public static PluginConfiguration Configuration => _config ??= ReadConfiguration();

    public static void SetConfigFilePath(string path)
    {
        _configFilePath = path;
    }

    private static PluginConfiguration ReadConfiguration()
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var input = File.ReadAllText(_configFilePath ?? Path.Join(AppDomain.CurrentDomain.BaseDirectory, "config.yaml"));

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
        if (!Directory.Exists(config.Game.TempPath))
        {
            Directory.CreateDirectory(config.Game.TempPath);
            Directory.CreateDirectory(Path.Join(config.Game.TempPath, "Guess"));
        }

        return config;
    }
}