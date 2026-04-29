using NLog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Marisa.Configuration;

public static class ConfigurationManager
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static PluginConfiguration? _config;

    private static string? _configFilePath;

    private static string? _configDirectory;

    public static PluginConfiguration Configuration => _config ??= ReadConfiguration();

    internal static string RequireString(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new MissingConfigurationException(key);
        return value;
    }

    internal static T RequireObject<T>(string key, T? value) where T : class
    {
        if (value is null) throw new MissingConfigurationException(key);
        return value;
    }

    internal static T[] RequireArray<T>(string key, T[]? value)
    {
        if (value is null || value.Length == 0) throw new MissingConfigurationException(key);
        return value;
    }

    internal static Dictionary<TKey, TValue> RequireDictionary<TKey, TValue>(string key, Dictionary<TKey, TValue>? value)
        where TKey : notnull
    {
        if (value is null || value.Count == 0) throw new MissingConfigurationException(key);
        return value;
    }

    public static void SetConfigFilePath(string path)
    {
        _configFilePath = path;
        _config = null;
    }

    private static PluginConfiguration ReadConfiguration()
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var configFilePath = _configFilePath ?? Path.Join(AppDomain.CurrentDomain.BaseDirectory, "config.yaml");
        _configDirectory = Path.GetDirectoryName(Path.GetFullPath(configFilePath));
        var input = File.ReadAllText(configFilePath);

        var config = deserializer.Deserialize<PluginConfiguration>(input);
        EnsureRequiredSections(config);
        ResolvePaths(config);
        WarnMissingConfiguration(config);

        Directory.CreateDirectory(config.TempPath);
        Directory.CreateDirectory(config.MaiMai.TempPath);
        Directory.CreateDirectory(config.Chunithm.TempPath);
        Directory.CreateDirectory(config.Ongeki.TempPath);
        Directory.CreateDirectory(config.Osu.TempPath);
        Directory.CreateDirectory(config.Arcaea.TempPath);
        Directory.CreateDirectory(config.Game.TempPath);
        Directory.CreateDirectory(Path.Join(config.Game.TempPath, "Guess"));

        var dbDirectory = Path.GetDirectoryName(config.DatabasePath);
        if (!string.IsNullOrEmpty(dbDirectory))
        {
            Directory.CreateDirectory(dbDirectory);
        }

        return config;
    }

    private static void ResolvePaths(PluginConfiguration config)
    {
        config.TempPath = ResolvePath(DefaultIfEmpty(config.TempPath, "temp"));
        config.ResourceRoot = string.IsNullOrWhiteSpace(config.ResourceRoot)
            ? ResolveDefaultResourceRoot()
            : ResolvePath(config.ResourceRoot);
        config.DatabasePath = ResolvePath(DefaultIfEmpty(config.DatabasePath, "bot.db"), config.TempPath);
        config.FfmpegPath = ResolvePath(config.FfmpegPath);

        ResolveGamePaths(config);
        ResolveResourcePaths(config);
    }

    private static void ResolveGamePaths(PluginConfiguration config)
    {
        config.Arcaea.TempPath = ResolvePath(DefaultIfEmpty(config.Arcaea.TempPath, "arcaea"), config.TempPath);
        config.Chunithm.TempPath = ResolvePath(DefaultIfEmpty(config.Chunithm.TempPath, "chunithm"), config.TempPath);
        config.Ongeki.TempPath = ResolvePath(DefaultIfEmpty(config.Ongeki.TempPath, "ongeki"), config.TempPath);
        config.Osu.TempPath = ResolvePath(DefaultIfEmpty(config.Osu.TempPath, "osu"), config.TempPath);
        config.MaiMai.TempPath = ResolvePath(DefaultIfEmpty(config.MaiMai.TempPath, "maimai"), config.TempPath);
        config.Game.TempPath = ResolvePath(DefaultIfEmpty(config.Game.TempPath, "game"), config.TempPath);
    }

    private static void ResolveResourcePaths(PluginConfiguration config)
    {
        config.MaiMai.ResourcePath = ResolvePath(DefaultIfEmpty(config.MaiMai.ResourcePath, "maimai"), config.ResourceRoot);
        config.Chunithm.ResourcePath = ResolvePath(DefaultIfEmpty(config.Chunithm.ResourcePath, "chunithm"), config.ResourceRoot);
        config.Ongeki.ResourcePath = ResolvePath(DefaultIfEmpty(config.Ongeki.ResourcePath, "ongeki"), config.ResourceRoot);
        config.Arcaea.ResourcePath = ResolvePath(DefaultIfEmpty(config.Arcaea.ResourcePath, "arcaea"), config.ResourceRoot);
        config.Arcaea.AssetsPath = ResolvePath(config.Arcaea.AssetsPath, config.ResourceRoot);

        config.Osu.ResourcePath = ResolvePath(DefaultIfEmpty(config.Osu.ResourcePath, "osu"), config.ResourceRoot);
        config.MaiMai.BeatMapPath = ResolvePath(config.MaiMai.BeatMapPath, config.ResourceRoot);
    }

    private static void EnsureRequiredSections(PluginConfiguration config)
    {
        config.Web ??= new WebConfiguration();
        config.NapCat ??= new NapCatConfiguration();
        config.DivingFish ??= new DivingFishConfiguration();
        config.Arcaea ??= new ArcaeaConfiguration();
        config.Chunithm ??= new ChunithmConfiguration();
        config.Ongeki ??= new OngekiConfiguration();
        config.Osu ??= new OsuConfiguration();
        config.MaiMai ??= new MaiMaiConfiguration();
        config.MaiMai.Secret ??= new MaiMaiSecret();
        config.Game ??= new GameConfiguration();
    }

    private static void WarnMissingConfiguration(PluginConfiguration config)
    {
        WarnIfEmpty("web.private", config.Web?.Private);
        WarnIfEmpty("web.public", config.Web?.Public);
        WarnIfEmpty("napCat.endpoint", config.NapCat?.Endpoint);
        WarnIfEmpty("napCat.token", config.NapCat?.Token);
        WarnIfEmpty("napCat.selfId", config.NapCat?.SelfId);
        WarnIfEmpty("osu.clientId", config.Osu?.ClientIdRaw);
        WarnIfEmpty("osu.clientSecret", config.Osu?.ClientSecretRaw);
        WarnIfEmpty("divingFish.devToken", config.DivingFish?.DevTokenRaw);
        WarnIfEmpty("chunithm.tokenLouis", config.Chunithm?.TokenLouisRaw);
        WarnIfEmpty("chunithm.rinNetKeyChip", config.Chunithm?.RinNetKeyChipRaw);
        WarnIfEmpty("chunithm.allNetKeyChip", config.Chunithm?.AllNetKeyChipRaw);
        WarnIfEmpty("maimai.secret.maiSalt", config.MaiMai?.Secret?.MaiSaltRaw);
        WarnIfEmpty("maimai.secret.aimeSalt", config.MaiMai?.Secret?.AimeSaltRaw);
        WarnIfEmpty("maimai.secret.aesKey", config.MaiMai?.Secret?.AesKeyRaw);
        WarnIfEmpty("maimai.secret.aesIv", config.MaiMai?.Secret?.AesIvRaw);
    }

    private static void WarnIfEmpty(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Logger.Warn($"Configuration `{key}` is empty; related features may be disabled or fail at runtime");
        }
    }

    private static string DefaultIfEmpty(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string ResolvePath(string? path, string? relativeTo = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return string.Empty;
        if (Path.IsPathFullyQualified(path)) return path;

        var basePath = relativeTo is null
            ? ResolveBasePath()
            : ResolvePath(relativeTo);

        return Path.GetFullPath(Path.Join(basePath, path));
    }

    private static string ResolveBasePath()
    {
        var candidates = new[]
        {
            _configDirectory,
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory()
        };

        foreach (var candidate in candidates.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var root = FindProjectRoot(candidate!);
            if (root is not null) return root;
        }

        return _configDirectory ?? AppContext.BaseDirectory;
    }

    private static string ResolveDefaultResourceRoot()
    {
        var basePath = ResolveBasePath();
        var candidates = new[]
        {
            Path.Join(basePath, "Marisa.Frontend", "public", "assets"),
            Path.Join(AppContext.BaseDirectory, "wwwroot", "assets"),
            Path.Join(_configDirectory ?? string.Empty, "wwwroot", "assets"),
            Path.Join(AppContext.BaseDirectory, "assets"),
            Path.Join(_configDirectory ?? string.Empty, "assets")
        };

        return candidates.FirstOrDefault(Directory.Exists) ?? candidates[0];
    }

    private static string? FindProjectRoot(string start)
    {
        var directory = new DirectoryInfo(start);

        while (directory is not null)
        {
            if (File.Exists(Path.Join(directory.FullName, "Marisa.sln")) || Directory.Exists(Path.Join(directory.FullName, "Marisa.StartUp")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
