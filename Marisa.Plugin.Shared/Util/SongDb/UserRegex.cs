using System.Text.RegularExpressions;

namespace Marisa.Plugin.Shared.Util.SongDb;

internal static class UserRegex
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(200);

    /// <summary>
    ///     用用户输入构造正则。带匹配超时以防止灾难性回溯（ReDoS）；不使用 Compiled，一次性模式无从摊销编译开销。
    /// </summary>
    public static Regex Create(string pattern)
    {
        return new Regex(pattern, RegexOptions.IgnoreCase, MatchTimeout);
    }
}
