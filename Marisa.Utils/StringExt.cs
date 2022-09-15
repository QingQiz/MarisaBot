using System.Text;
using System.Text.RegularExpressions;
using SixLabors.Fonts;

namespace Marisa.Utils;

public static class StringExt
{
    /// <summary>
    /// 测量文本大小
    /// </summary>
    /// <param name="text"></param>
    /// <param name="font"></param>
    /// <returns></returns>
    public static FontRectangle Measure(this string text, Font font)
    {
        var option = ImageDraw.GetTextOptions(font);

        return TextMeasurer.MeasureBounds(text, option);
    }

    public static FontRectangle MeasureWithSpace(this string text, Font font)
    {
        var option = ImageDraw.GetTextOptions(font);

        return TextMeasurer.Measure(text, option);
    }

    /// <summary>
    /// 和原始的 string.StartWith 类似，只不过同时检查多个
    /// </summary>
    /// <param name="str"></param>
    /// <param name="prefixes"></param>
    /// <param name="comparer"></param>
    /// <returns></returns>
    public static bool StartWith(this string str, IEnumerable<string> prefixes,
        StringComparison comparer = StringComparison.Ordinal)
    {
        return prefixes.Any(p => str.StartsWith(p, comparer));
    }

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

    /// <summary>
    /// https://stackoverflow.com/a/14087738/13442887
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string Escape(this string input)
    {
        var literal = new StringBuilder(input.Length + 2);
        foreach (var c in input) {
            switch (c) {
                case '\"': literal.Append("\\\""); break;
                case '\\': literal.Append(@"\\"); break;
                case '\0': literal.Append(@"\0"); break;
                case '\a': literal.Append(@"\a"); break;
                case '\b': literal.Append(@"\b"); break;
                case '\f': literal.Append(@"\f"); break;
                case '\n': literal.Append(@"\n"); break;
                case '\r': literal.Append(@"\r"); break;
                case '\t': literal.Append(@"\t"); break;
                case '\v': literal.Append(@"\v"); break;
                default:
                    literal.Append(c);
                    // // ASCII printable character
                    // if (c >= 0x20 && c <= 0x7e) {
                    //     literal.Append(c);
                    //     // As UTF16 escaped character
                    // } else {
                    //     literal.Append(@"\u");
                    //     literal.Append(((int)c).ToString("x4"));
                    // }
                    break;
            }
        }
        return literal.ToString();
    }
}