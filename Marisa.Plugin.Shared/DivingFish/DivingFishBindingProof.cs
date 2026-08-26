using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Marisa.Plugin.Shared.DivingFish;

/// <summary>
///     水鱼绑定的一次性证明（OAuth 授权码回调后生成）。
///     证明码 C 只在浏览器响应正文显示一次，数据库只存 SHA-256 hash，5 分钟过期。
///     用户须由原 QQ 在原群提交 C 才能完成绑定，防远程钓鱼（Remote Phishing）。
/// </summary>
public static class DivingFishBindingProof
{
    private const int MaxTtlSeconds = 300; // 5 分钟

    private static readonly ConcurrentDictionary<string, ProofEntry> Store = new();

    public sealed class ProofEntry
    {
        public required string CodeHash { get; init; }
        public required long Qq { get; init; }
        public required long GroupId { get; init; }
        public required string Sub { get; init; }
        public required string Username { get; init; }
        public required string Game { get; init; }
        public required string Scope { get; init; }
        public DateTime ExpiresAt { get; init; } = DateTime.UtcNow.AddSeconds(MaxTtlSeconds);
        public int Generation { get; init; }
        public bool Consumed { get; set; }
        public bool Compromised { get; set; }
    }

    /// <summary>
    ///     生成一次性证明码 C（至少 128-bit 随机），仅返回明文，hash 存内存。
    /// </summary>
    public static string Issue(string qq, string groupId, string sub, string username, string game, string scope, int generation)
    {
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)); // 128-bit
        Store[code] = new ProofEntry
        {
            CodeHash = Sha256Hex(code),
            Qq = long.TryParse(qq, out var q) ? q : 0,
            GroupId = long.TryParse(groupId, out var g) ? g : 0,
            Sub = sub,
            Username = username,
            Game = game,
            Scope = scope,
            Generation = generation
        };
        return code;
    }

    /// <summary>
    ///     校验并原子消费证明码。成功返回证明条目，失败返回 null。
    ///     校验：存在、未过期、hash 匹配、未被消费。
    /// </summary>
    public static ProofEntry? Consume(string code, long senderQq, long groupId)
    {
        if (!Store.TryGetValue(code, out var entry)) return null;
        if (DateTime.UtcNow >= entry.ExpiresAt) { Store.TryRemove(code, out _); return null; }
        if (entry.Consumed) return null;
        if (!entry.CodeHash.Equals(Sha256Hex(code), StringComparison.OrdinalIgnoreCase)) return null;

        // 原子消费：标记后再校验 QQ/群（防竞态）
        entry.Consumed = true;

        if (entry.Qq != senderQq || entry.GroupId != groupId)
        {
            entry.Compromised = true;
            Store.TryRemove(code, out _);
            return null;
        }

        Store.TryRemove(code, out _);
        return entry;
    }

    private static string Sha256Hex(string input)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }
}
