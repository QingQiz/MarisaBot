using System.Security.Cryptography;
using System.Text;
using SixLabors.Fonts;

namespace Marisa.Plugin.Shared.Util;

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

    public static string ToHalfWidth(this string input)
    {
        var result = new char[input.Length];
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i] switch
            {
                // 处理全角大写英文字母, 处理全角小写英文字母, 处理全角数字  
                >= 'Ａ' and <= 'Ｚ' or >= 'ａ' and <= 'ｚ' or >= '０' and <= '９' => (char)(input[i] - 0xFEE0),
                // 处理全角空格  
                '　' => ' ',
                _   => input[i]
            };
            result[i] = c;
        }
        return new string(result);
    }
}