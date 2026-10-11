using System.Dynamic;
using Newtonsoft.Json.Linq;

namespace Marisa.Plugin.Shared.MaiMaiDx;

/// <summary>把落雪公共曲库的真实谱面资料转换为本地完整歌曲 ID；不生成查分器谱面 ID 或物量。</summary>
public static class LxnsSongCatalog
{
    public static List<MaiMaiSong> FromJson(string json)
    {
        var root = JObject.Parse(json);
        var versions = (root["versions"] as JArray ?? [])
            .OfType<JObject>()
            .Select(version => (Number: (int)version["version"]!, Title: (string)version["title"]!))
            .OrderBy(version => version.Number)
            .ToList();
        if (versions.Count == 0) throw new InvalidDataException("Lxns song list has no version metadata");

        var result = new List<MaiMaiSong>();
        foreach (var song in (root["songs"] as JArray ?? []).OfType<JObject>())
        {
            var rawId = (long)song["id"]!;
            if (song["difficulties"] is not JObject difficulties) continue;

            Add("standard", "SD", rawId);
            Add("dx", "DX", rawId + 10000);
            if (rawId > 100000) Add("utage", "DX", rawId);

            void Add(string sourceType, string type, long id)
            {
                if (sourceType != "utage" && rawId is <= 0 or >= 10000) return;
                if (difficulties[sourceType] is not JArray { Count: > 0 } source) return;

                var charts = source.OfType<JObject>().OrderBy(chart => (int)chart["difficulty"]!).ToList();
                if (charts.Count != source.Count) return;
                if (sourceType != "utage" && (charts.Count is < 4 or > 5 ||
                    charts.Where((chart, index) => (int)chart["difficulty"]! != index).Any())) return;

                var normalizedCharts = new JArray();
                var levels = new JArray();
                var constants = new JArray();
                foreach (var chart in charts)
                {
                    if (chart["notes"] is not JObject notes) return;
                    var sides = sourceType == "utage" && (bool?)chart["is_buddy"] == true
                        ? new[] { notes["left"] as JObject, notes["right"] as JObject }
                        : new[] { notes };
                    foreach (var side in sides)
                    {
                        if (!TryReadNotes(side, type, out var noteCounts)) return;
                        if (chart["level"] is not JValue { Type: JTokenType.String } ||
                            chart["level_value"]?.Type is not (JTokenType.Integer or JTokenType.Float)) return;
                        normalizedCharts.Add(new JObject
                        {
                            ["notes"] = noteCounts,
                            ["charter"] = (string?)chart["note_designer"] ?? ""
                        });
                        levels.Add(chart["level"]!.DeepClone());
                        constants.Add(chart["level_value"]!.DeepClone());
                    }
                }
                if (normalizedCharts.Count == 0 || normalizedCharts.Count > MaiMaiSong.LevelNameAll.Count) return;

                // version 是该谱面首次出现版本。同曲的 SD/DX 可分别推出，后添 Re:MASTER
                // 也不应改变基础四谱所属的版本或 NEW 状态，因此按当前类型的基础谱判定。
                var originalCharts = sourceType == "utage" ? charts : charts.Take(4);
                var chartVersion = originalCharts.Min(chart => (int)chart["version"]!);
                var version = versions.LastOrDefault(item => item.Number <= chartVersion);
                if (version.Title == null) return;
                var normalized = new JObject
                {
                    ["id"] = id.ToString(),
                    ["title"] = song["title"]!.DeepClone(),
                    ["type"] = type,
                    ["ds"] = constants,
                    ["level"] = levels,
                    ["charts"] = normalizedCharts,
                    ["basic_info"] = new JObject
                    {
                        ["title"] = song["title"]!.DeepClone(),
                        ["artist"] = song["artist"]!.DeepClone(),
                        ["genre"] = song["genre"]!.DeepClone(),
                        ["bpm"] = song["bpm"]!.DeepClone(),
                        ["release_date"] = "",
                        // 保留落雪公布的国服版本名，不根据年份猜测日服版本。
                        ["from"] = version.Title,
                        ["is_new"] = version.Number == versions[^1].Number
                    }
                };
                result.Add(new MaiMaiSong(normalized.ToObject<ExpandoObject>()!));
            }
        }
        return result;
    }

    private static bool TryReadNotes(JObject? notes, string type, out JArray counts)
    {
        counts = [];
        if (notes == null) return false;
        var names = type == "DX"
            ? new[] { "tap", "hold", "slide", "touch", "break" }
            : new[] { "tap", "hold", "slide", "break" };
        foreach (var name in names)
        {
            if (notes[name]?.Type != JTokenType.Integer || (long)notes[name]! < 0) return false;
            counts.Add(notes[name]!.DeepClone());
        }
        return counts.Values<long>().Sum() > 0;
    }
}
