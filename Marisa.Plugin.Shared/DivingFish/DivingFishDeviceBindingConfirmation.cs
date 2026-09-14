using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Marisa.Plugin.Shared.DivingFish;

public static class DivingFishDeviceBindingConfirmation
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);
    private static readonly ConcurrentDictionary<string, Entry> Store = new();

    public enum ConsumeStatus
    {
        Success,
        NotFound,
        WrongUser,
        Expired
    }

    public sealed record Entry(
        long Qq,
        string Sub,
        string Game,
        string Scope,
        DateTimeOffset ExpiresAt);

    public readonly record struct ConsumeResult(ConsumeStatus Status, Entry? Entry)
    {
        public bool IsSuccess => Status == ConsumeStatus.Success && Entry is not null;
    }

    public static string Issue(long qq, string sub, string game, string scope)
    {
        if (qq <= 0) throw new ArgumentOutOfRangeException(nameof(qq));
        if (string.IsNullOrWhiteSpace(sub)) throw new ArgumentException("水鱼 sub 不能为空", nameof(sub));
        if (string.IsNullOrWhiteSpace(game)) throw new ArgumentException("游戏不能为空", nameof(game));
        if (string.IsNullOrWhiteSpace(scope)) throw new ArgumentException("scope 不能为空", nameof(scope));

        string code;
        do
        {
            code = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        } while (!Store.TryAdd(Hash(code), new Entry(qq, sub, game, scope, DateTimeOffset.UtcNow.Add(Lifetime))));

        return code;
    }

    public static ConsumeResult Consume(string code, long qq)
    {
        if (qq <= 0 || string.IsNullOrWhiteSpace(code)) return new ConsumeResult(ConsumeStatus.NotFound, null);
        if (!Store.TryGetValue(Hash(code.Trim().ToUpperInvariant()), out var entry))
        {
            return new ConsumeResult(ConsumeStatus.NotFound, null);
        }

        if (DateTimeOffset.UtcNow >= entry.ExpiresAt)
        {
            Store.TryRemove(Hash(code.Trim().ToUpperInvariant()), out _);
            return new ConsumeResult(ConsumeStatus.Expired, null);
        }

        return entry.Qq == qq
            ? Store.TryRemove(Hash(code.Trim().ToUpperInvariant()), out var removed)
                ? new ConsumeResult(ConsumeStatus.Success, removed)
                : new ConsumeResult(ConsumeStatus.NotFound, null)
            : new ConsumeResult(ConsumeStatus.WrongUser, null);
    }

    private static string Hash(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}
