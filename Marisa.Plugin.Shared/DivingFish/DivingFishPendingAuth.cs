using System.Collections.Concurrent;

namespace Marisa.Plugin.Shared.DivingFish;

/// <summary>
///     授权码流程的待确认状态：state → (QQ, 群, verifier, game, generation)。
///     callback 验证 state 后从中取 verifier 换码，并生成一次性证明。
/// </summary>
public static class DivingFishPendingAuth
{
    private const int MaxTtlSeconds = 600; // 10 分钟

    private static readonly ConcurrentDictionary<string, PendingEntry> Store = new();

    public sealed class PendingEntry
    {
        public required long Qq { get; init; }
        public required long GroupId { get; init; }
        public required string CodeVerifier { get; init; }
        public required string Game { get; init; }
        public int Generation { get; init; }
        public DateTime ExpiresAt { get; init; } = DateTime.UtcNow.AddSeconds(MaxTtlSeconds);
    }

    public static void Add(string state, long qq, long groupId, string verifier, string game, int generation)
    {
        Store[state] = new PendingEntry
        {
            Qq = qq,
            GroupId = groupId,
            CodeVerifier = verifier,
            Game = game,
            Generation = generation
        };
    }

    public static PendingEntry? Take(string state)
    {
        if (!Store.TryGetValue(state, out var entry)) return null;
        if (DateTime.UtcNow >= entry.ExpiresAt) { Store.TryRemove(state, out _); return null; }
        Store.TryRemove(state, out _);
        return entry;
    }
}
