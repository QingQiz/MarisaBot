using System;
using Realms;

namespace Marisa.Database.Entity;

public partial class OpenAiUsageRecord : IRealmObject, IHaveId
{
    [PrimaryKey]
    public long Id { get; set; }

    [Indexed]
    public long UserId { get; set; }

    public string Model { get; set; }

    public string SystemPrompt { get; set; }

    public string UserPrompt { get; set; }

    public string Output { get; set; }

    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public int TotalTokens { get; set; }

    public int? ReasoningTokens { get; set; }

    public DateTimeOffset Timestamp { get; set; }
}
