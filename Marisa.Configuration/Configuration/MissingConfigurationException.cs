namespace Marisa.Configuration;

public sealed class MissingConfigurationException(string key)
    : InvalidOperationException($"Configuration `{key}` is missing")
{
    public string Key { get; } = key;

    public string UserMessage => $"该功能未配置，请联系管理员检查配置项：{Key}";
}
