namespace Marisa.Plugin.Shared.Interface;

public interface IOpenAiClient
{
    Task<string> ChatAsync(string systemPrompt, string userMessage, CancellationToken cancellationToken = default);
}
