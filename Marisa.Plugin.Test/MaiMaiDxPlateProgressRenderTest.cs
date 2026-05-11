using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Flurl.Http;
using Marisa.Configuration;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.Util.SongDb;
using NUnit.Framework;
using SixLabors.ImageSharp;

namespace Marisa.Plugin.Test;

/// <summary>
///     端到端渲染验证。Explicit：默认不跑（要联网拉 diving-fish music_data；
///     生成的 PNG 会丢到当前 cwd 旁边的输出文件夹里，方便人工对眼睛）。
/// </summary>
[Explicit("Generates PNGs locally; needs internet + maimai resources under Marisa.Frontend/public/assets/maimai")]
public class MaiMaiDxPlateProgressRenderTest
{
    private SongDb<MaiMaiSong> _songDb = null!;
    private string _outputDir = null!;

    [SetUp]
    public void SetUp()
    {
        var configPath = Path.Join(
            Directory.GetParent(Environment.CurrentDirectory)!.Parent!.Parent!.Parent!.ToString(),
            "Marisa.StartUp", "config.yaml");
        ConfigurationManager.SetConfigFilePath(configPath);

        _songDb = new SongDb<MaiMaiSong>(
            ResourceManager.ResourcePath + "/aliases.tsv",
            ResourceManager.TempPath + "/MaiMaiSongAliasTemp.txt",
            () =>
            {
                var data = "https://www.diving-fish.com/api/maimaidxprober/music_data".GetJsonListAsync().Result;
                return data.Select(d => new MaiMaiSong(d)).ToList();
            }
        );

        _outputDir = Environment.GetEnvironmentVariable("PLATE_OUT")
                  ?? Path.Join(Path.GetTempPath(), "marisabot-plate-progress");
        Directory.CreateDirectory(_outputDir);
        TestContext.Out.WriteLine($"output dir: {_outputDir}");
    }

    /// <summary>
    ///     构造 mock score。strategy = (songId, levelIdx) -> SongScore?。返回 null 代表玩家没打过。
    /// </summary>
    private static IReadOnlyDictionary<(long, int), SongScore> BuildMockScores(
        IEnumerable<(double Constant, int LevelIdx, MaiMaiSong Song)> charts,
        Func<long, int, SongScore?> strategy)
    {
        var d = new Dictionary<(long, int), SongScore>();
        foreach (var (_, levelIdx, song) in charts)
        {
            var s = strategy(song.Id, levelIdx);
            if (s != null) d[(song.Id, levelIdx)] = s;
        }
        return d;
    }

    private List<(double Constant, int LevelIdx, MaiMaiSong Song)> SelectFor(PlateData.Query q)
    {
        // 跟 MaiMaiDx.SelectChartsForQuery 同语义：默认 MASTER (i=3)，可被 query.LevelIdx 覆盖
        var allCharts = _songDb.SongList
            .SelectMany(song => song.Constants.Select((constant, i) => (constant, i, song)))
            .Where(t => t.i == q.LevelIdx);

        return q.Selector switch
        {
            PlateData.Selector.Plate p => allCharts
                .Where(t => p.Versions.Any(v => string.Equals(v, t.song.Version, StringComparison.OrdinalIgnoreCase)))
                .Select(t => (t.constant, t.i, t.song))
                .ToList(),
            PlateData.Selector.Charter c => allCharts
                .Where(t => t.i < t.song.Charters.Count && string.Equals(t.song.Charters[t.i], c.Name, StringComparison.OrdinalIgnoreCase))
                .Select(t => (t.constant, t.i, t.song))
                .ToList(),
            PlateData.Selector.Genre g => allCharts
                .Where(t => string.Equals(t.song.Info.Genre, g.FullName, StringComparison.Ordinal))
                .Select(t => (t.constant, t.i, t.song))
                .ToList(),
            _ => [],
        };
    }

    private static SongScore MakeScore(long id, int levelIdx, double ach, string fc = "", string fs = "") => new()
    {
        Id          = id,
        LevelIdx    = levelIdx,
        Achievement = ach,
        Constant    = 0,
        Fc          = fc,
        Fs          = fs,
    };

    private void RenderAndSave(
        string raw,
        Func<long, int, SongScore?> scoreStrategy,
        int? iconSize = null,
        Color? stampColor = null,
        float? stampOpacity = null,
        string? suffix = null)
    {
        var charters = _songDb.SongList
            .SelectMany(s => s.Charters)
            .Where(c => !string.IsNullOrWhiteSpace(c) && c != "-" && c != "N/A")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.That(PlateData.TryParse(raw, charters, out var query, out var error), Is.True, $"parse failed: {error?.Kind}/{error?.Detail}");

        var pairs = SelectFor(query!);
        TestContext.Out.WriteLine($"{raw}: selector={query!.Selector}, threshold={query.Threshold.DisplayName}, levelIdx={query.LevelIdx}, pairs={pairs.Count}");
        Assert.That(pairs.Count, Is.GreaterThan(0), "no charts matched");

        var scores = BuildMockScores(pairs, scoreStrategy);

        // 用 DrawPlateProgress 的内置默认（marker=75, alpha=0.40），除非调用方显式覆盖
        var img = MaiMaiDraw.DrawPlateProgress(
            query, pairs, scores, raw,
            iconMarkerHeight: iconSize ?? 75,
            stampColor: stampColor,
            stampOpacity: stampOpacity ?? 0.40f);

        var safe = string.Concat(raw.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_'));
        var fileName = suffix is null ? $"{safe}.png" : $"{safe}.{suffix}.png";
        var path = Path.Join(_outputDir, fileName);
        img.SaveAsPng(path);
        TestContext.Out.WriteLine($"saved {path} ({pairs.Count} cells)");
    }

    // 下面 7 个 [Test] 是用户钦定的字面命令，请勿改动文本（要的就是这几条作图）。

    [Test]
    public void Render_真将完成表()
    {
        var rng = new Random(11);
        RenderAndSave("真将完成表", (id, idx) =>
        {
            var roll = rng.NextDouble();
            if (roll < 0.45) return null;
            if (roll < 0.65) return MakeScore(id, idx, 97 + rng.NextDouble());
            if (roll < 0.80) return MakeScore(id, idx, 99 + rng.NextDouble());
            return MakeScore(id, idx, 100 + rng.NextDouble() * 1.4);
        });
    }

    [Test]
    public void Render_真代EXPERT将完成表()
    {
        // difficulty(EXPERT) 在 threshold(将) 之前 — 验证字段位置可任意。
        var rng = new Random(31);
        RenderAndSave("真代EXPERT将完成表", (id, idx) =>
        {
            var roll = rng.NextDouble();
            if (roll < 0.25) return null;
            if (roll < 0.45) return MakeScore(id, idx, 96.5 + rng.NextDouble());
            if (roll < 0.70) return MakeScore(id, idx, 99 + rng.NextDouble());
            if (roll < 0.88) return MakeScore(id, idx, 100 + rng.NextDouble() * 0.4);
            return MakeScore(id, idx, 100.5 + rng.NextDouble() * 0.4);
        });
    }

    [Test]
    public void Render_真神白谱完成表()
    {
        var rng = new Random(23);
        RenderAndSave("真神白谱完成表", (id, idx) =>
        {
            var roll = rng.NextDouble();
            if (roll < 0.35) return null;
            if (roll < 0.60) return MakeScore(id, idx, 99, "fc");
            if (roll < 0.80) return MakeScore(id, idx, 100, "ap");
            return MakeScore(id, idx, 100.5, "app");
        });
    }

    [Test]
    public void Render_熊代理论值完成表()
    {
        var rng = new Random(7);
        RenderAndSave("熊代理论值完成表", (id, idx) =>
        {
            var roll = rng.NextDouble();
            if (roll < 0.35) return null;
            if (roll < 0.55) return MakeScore(id, idx, 99, "fc");
            if (roll < 0.70) return MakeScore(id, idx, 99.5, "fcp");
            if (roll < 0.85) return MakeScore(id, idx, 100.5, "ap");
            return MakeScore(id, idx, 101, "app");
        });
    }

    [Test]
    public void Render_镜大将完成表()
    {
        // 注意没"代"字 — "镜大将" 直接解析为 plate(镜) + threshold(大将=SSS+)
        var rng = new Random(42);
        RenderAndSave("镜大将完成表", (id, idx) =>
        {
            var roll = rng.NextDouble();
            if (roll < 0.30) return null;
            if (roll < 0.50) return MakeScore(id, idx, 99.5 + rng.NextDouble() * 0.5);
            if (roll < 0.75) return MakeScore(id, idx, 100.0 + rng.NextDouble() * 0.4);
            return MakeScore(id, idx, 100.5 + rng.NextDouble() * 0.5);
        });
    }

    [Test]
    public void Render_翠楼屋神完成表()
    {
        var rng = new Random(99);
        RenderAndSave("翠楼屋神完成表", (id, idx) =>
        {
            var roll = rng.NextDouble();
            if (roll < 0.40) return null;
            if (roll < 0.65) return MakeScore(id, idx, 98 + rng.NextDouble());
            if (roll < 0.85) return MakeScore(id, idx, 100, "ap");
            return MakeScore(id, idx, 100.5, "app");
        });
    }

    [Test]
    public void Render_术力口舞舞完成表()
    {
        var rng = new Random(13);
        RenderAndSave("术力口舞舞完成表", (id, idx) =>
        {
            var roll = rng.NextDouble();
            if (roll < 0.55) return null;
            if (roll < 0.70) return MakeScore(id, idx, 100, "ap", "fs");
            if (roll < 0.85) return MakeScore(id, idx, 100, "ap", "fdx");
            return MakeScore(id, idx, 100.5, "app", "fdxp");
        });
    }

}
