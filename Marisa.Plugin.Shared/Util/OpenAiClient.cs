using System.Security.Cryptography;
using System.Text;
using Flurl.Http;
using Marisa.Configuration;
using Marisa.Database;
using Marisa.Database.Entity;
using Newtonsoft.Json.Linq;

namespace Marisa.Plugin.Shared.Util;

public class OpenAiClient
{
    public static readonly OpenAiClient Default = new OpenAiClient();

    public async Task<string> ChatAsync(string systemPrompt, string userMessage, string? model = null, string? userId = null, ThinkingMode thinking = ThinkingMode.Default, CancellationToken cancellationToken = default)
    {
        var cfg = ConfigurationManager.Configuration.OpenAi;
        model ??= cfg.Model;
        userId ??= Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(systemPrompt)));

        var messages = new[]
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = userMessage }
        };

        var (thinkType, effort) = GetThinkingParams();

        object request = (thinkType, effort) switch
        {
            (null, _)     => new { model, messages, user_id = userId },
            (_,    null)  => new { model, messages, thinking = new { type = thinkType }, user_id = userId },
            _             => new { model, messages, thinking = new { type = thinkType }, reasoning_effort = effort, user_id = userId },
        };

        var response = await $"{cfg.Endpoint.TrimEnd('/')}/chat/completions"
            .WithHeader("Authorization", $"Bearer {cfg.ApiKey}")
            .PostJsonAsync(request, cancellationToken);

        var json = JObject.Parse(await response.GetStringAsync());

        var output = json["choices"]?[0]?["message"]?["content"]?.Value<string>()?.Trim()
                     ?? throw new InvalidOperationException("OpenAI API returned empty response");

        RecordUsage(json, model, systemPrompt, userMessage, output, userId);

        return output;

        (string? thinkType, string? effort) GetThinkingParams() => thinking switch
        {
            ThinkingMode.Disabled => ("disabled", null),
            ThinkingMode.Low     => ("enabled", "low"),
            ThinkingMode.Medium  => ("enabled", "medium"),
            ThinkingMode.High    => ("enabled", "high"),
            ThinkingMode.Max     => ("enabled", "max"),
            ThinkingMode.XHigh   => ("enabled", "xhigh"),
            _ => (null, null)
        };

        void RecordUsage(JObject root, string usedModel, string prompt, string userPrompt, string output, string uid)
        {
            var usage = root["usage"];
            if (usage is null) return;

            using var realm = BotDbContext.OpenRealm();
            realm.Write(() =>
            {
                realm.AddWithAutoId(new OpenAiUsageRecord
                {
                    UserId           = uid,
                    Model            = usedModel,
                    SystemPrompt     = prompt,
                    UserPrompt       = userPrompt,
                    Output           = output,
                    PromptTokens     = usage["prompt_tokens"]?.Value<int>() ?? 0,
                    CompletionTokens = usage["completion_tokens"]?.Value<int>() ?? 0,
                    TotalTokens      = usage["total_tokens"]?.Value<int>() ?? 0,
                    ReasoningTokens  = usage["completion_tokens_details"]?["reasoning_tokens"]?.Value<int>(),
                    Timestamp        = DateTimeOffset.UtcNow,
                });
            });
        }
    }
}
