using Flurl.Http;
using Marisa.Configuration;
using Marisa.Plugin.Shared.Interface;
using Newtonsoft.Json.Linq;

namespace Marisa.Plugin.Shared.Util;

public class OpenAiClient : IOpenAiClient
{
    public static readonly IOpenAiClient Default = new OpenAiClient();

    public async Task<string> ChatAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken = default)
    {
        var cfg = ConfigurationManager.Configuration.OpenAi;

        var request = new
        {
            model = cfg.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userMessage }
            }
        };

        var response = await $"{cfg.Endpoint.TrimEnd('/')}/chat/completions"
            .WithHeader("Authorization", $"Bearer {cfg.ApiKey}")
            .PostJsonAsync(request, cancellationToken);

        var json = JObject.Parse(await response.GetStringAsync());
        return json["choices"]?[0]?["message"]?["content"]?.Value<string>()?.Trim()
               ?? throw new InvalidOperationException("OpenAI API returned empty response");
    }
}
