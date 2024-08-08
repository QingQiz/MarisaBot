using System.Security.Cryptography;
using System.Text;
using SixLabors.Fonts;

namespace Marisa.Utils;

public static class StringExt
{
    public static string GetMd5Hash(this string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        var textData = Encoding.UTF8.GetBytes(text);
        var hash     = MD5.HashData(textData);

        return BitConverter.ToString(hash).Replace("-", string.Empty);
    }

    public static string GetSha256Hash(this string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        using var sha = SHA256.Create();

        var textData = Encoding.UTF8.GetBytes(text);
        var hash     = sha.ComputeHash(textData);

        return BitConverter.ToString(hash).Replace("-", string.Empty);
    }

    /// <summary>
    ///     测量文本大小
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

        return TextMeasurer.MeasureAdvance(text, option);
    }

    /// <summary>
    ///     https://stackoverflow.com/a/14087738/13442887
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public static string Escape(this string input)
    {
        var literal = new StringBuilder(input.Length + 2);
        foreach (var c in input)
        {
            switch (c)
            {
                case '\"':
                    literal.Append("\\\"");
                    break;
                case '\\':
                    literal.Append(@"\\");
                    break;
                case '\0':
                    literal.Append(@"\0");
                    break;
                case '\a':
                    literal.Append(@"\a");
                    break;
                case '\b':
                    literal.Append(@"\b");
                    break;
                case '\f':
                    literal.Append(@"\f");
                    break;
                case '\n':
                    literal.Append(@"\n");
                    break;
                case '\r':
                    literal.Append(@"\r");
                    break;
                case '\t':
                    literal.Append(@"\t");
                    break;
                case '\v':
                    literal.Append(@"\v");
                    break;
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