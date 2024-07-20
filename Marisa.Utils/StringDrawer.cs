using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Marisa.Utils;

public class StringDrawer
{
    private readonly float _lineSpace;

    public StringDrawer(float lineSpace = 0)
    {
        _lineSpace = lineSpace;
    }

    private readonly List<List<(string Text, Font Font, Color Color )>> _textCache = new();
    private List<List<FontRectangle>>? _textMeasure;
    private readonly List<(float X, float Y)> _position = new();

    private bool _newLine = true;

    /// <summary>
    /// 添加一堆各不相同的字符串
    /// </summary>
    /// <param name="strings"></param>
    public void Add(params (string, Font, Color)[] strings)
    {
        foreach (var valueTuple in strings)
        {
            Add(valueTuple.Item1, valueTuple.Item2, valueTuple.Item3);
        }
    }

    /// <summary>
    /// 添加一堆相同字体和颜色的字符串
    /// </summary>
    /// <param name="f"></param>
    /// <param name="b"></param>
    /// <param name="text"></param>
    public void Add(Font f, Color b, params string[] text)
    {
        foreach (var t in text)
        {
            Add(t, f, b);
        }
    }

    /// <summary>
    /// 添加一堆相同字体不同颜色的字符串
    /// </summary>
    /// <param name="f"></param>
    /// <param name="strings"></param>
    public void Add(Font f, params (string, Color)[] strings)
    {
        foreach (var s in strings)
        {
            Add(s.Item1, f, s.Item2);
        }
    }

    /// <summary>
    /// 添加一堆相同颜色不同字体的字符串
    /// </summary>
    /// <param name="b"></param>
    /// <param name="strings"></param>
    public void Add(Color b, params (string, Font)[] strings)
    {
        foreach (var s in strings)
        {
            Add(s.Item1, s.Item2, b);
        }
    }

    /// <summary>
    /// 添加一个字符串
    /// </summary>
    /// <param name="t"></param>
    /// <param name="f"></param>
    /// <param name="b"></param>
    public void Add(string t, Font f, Color b)
    {
        _textMeasure = null;
        // 新创建一行
        if (_newLine)
        {
            _textCache.Add([]);
            _newLine = false;
        }

        // 忽略空字符串
        if (string.IsNullOrEmpty(t)) return;

        // 没有换行的直接添加
        if (!t.Contains('\n'))
        {
            _textCache.Last().Add((t, f, b));
            return;
        }

        // 分割字符串到多行
        // NOTE 使用换行分割以后，最后一串字符串的不会创建新行
        foreach (var l in t.Replace("\r\n", "\n").Split('\n'))
        {
            Add(l, f, b);
            _newLine = true;
        }

        _newLine = false;
    }

    private List<List<FontRectangle>> MeasureText()
    {
        return _textCache
            .Select(tc => tc
                .Select(t => t.Text.MeasureWithSpace(t.Font))
                .ToList())
            .ToList();
    }

    public SizeF Measure()
    {
        _textMeasure ??= MeasureText();

        var noneEmptyLineHeights = _textMeasure
            .Where(ms => ms.Any())
            .Select(ms => ms.Max(m => m.Height))
            .ToList();

        var noneEmptyLineWidth = _textMeasure
            .Where(ms => ms.Any())
            .Select(ms => ms.Sum(m => m.Width))
            .Max();

        var emptyLineCount = _textMeasure.Count(ms => !ms.Any());

        var avgH = noneEmptyLineHeights.Average();
        var h    = noneEmptyLineHeights.Sum() + emptyLineCount * avgH + _lineSpace * (_textCache.Count - 1);

        return new SizeF(noneEmptyLineWidth, h);
    }

    public void Draw(Image image, float posX = 0, float posY = 0)
    {
        _textMeasure ??= MeasureText();

        // average height of all none empty lines
        var avgH = _textMeasure
            .Where(ms => ms.Any())
            .Select(ms => ms.MaxBy(m => m.Height))
            .Average(m => m.Height);

        float y = 0;

        for (var i = 0; i < _textCache.Count; i++)
        {
            var line    = _textCache[i];
            var measure = _textMeasure[i];

            float x = 0;
            if (!line.Any())
            {
                y += avgH + _lineSpace;
                continue;
            }

            // line height
            var lineH = measure.MaxBy(m => m.Height).Height;

            for (var j = 0; j < _textCache[i].Count; j++)
            {
                // config to draw
                var c = _textCache[i][j];
                // text measure
                var m = _textMeasure[i][j];

                var x1 = x;
                var y1 = y;
                image.Mutate(im => im.DrawText(c.Text, c.Font, c.Color, posX + x1, posY + y1 - (lineH - m.Height)));

                // update x to next point
                x += m.Width;
            }

            // update y to next line
            y += lineH + _lineSpace;
        }
    }
}