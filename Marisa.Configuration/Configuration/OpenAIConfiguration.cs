#pragma warning disable CS8618

namespace Marisa.Configuration;

public class OpenAIConfiguration
{
    private string? _apiKey;

    public string Endpoint { get; set; }

    public string Model { get; set; }

    public string ApiKey
    {
        get => ConfigurationManager.RequireString("openai.apiKey", _apiKey);
        set => _apiKey = value;
    }

    internal string? ApiKeyRaw => _apiKey;
}
