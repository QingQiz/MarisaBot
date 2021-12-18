using System.Text.RegularExpressions;

namespace QQBOT.Plugin.Shared.Util
{
    public static class StringExt
    {
        /// <summary>
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="prefixes"></param>
        /// <returns>return null if trim nothing</returns>
        public static string? TrimStart(this string msg, IEnumerable<string> prefixes)
        {
            msg = msg.Trim();

            return (from prefix in prefixes
                where msg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                select msg[prefix.Length..].Trim()).FirstOrDefault();
        }

        public static string? TrimStart(this string msg, string prefix)
        {
            msg = msg.Trim();

            return msg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? msg[prefix.Length..] : null;
        }

        public static IEnumerable<(string Prefix, int Index)> CheckPrefix(this string msg, IEnumerable<string> prefixes)
        {
            return prefixes.Select((p, i) => (p, i))
                .Where(x => msg.StartsWith(x.p, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsRegex(this string regex)
        {
            try
            {
                var _ = Regex.Match("", regex);
                return true;
            }
            catch (RegexParseException)
            {
                return false;
            }
        }
    }
}