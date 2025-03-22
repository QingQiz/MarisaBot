using Newtonsoft.Json;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace Marisa.Plugin.Shared.MaiMaiDx;

public class SongScore
{
    [JsonProperty("achievements")]
    public double Achievement { get; set; }

    [JsonProperty("ds")]
    public double Constant { get; set; }

    [JsonProperty("dxScore")]
    public int DxScore { get; set; }

    [JsonProperty("fc")]
    public string Fc { get; set; }

    [JsonProperty("fs")]
    public string Fs { get; set; }

    [JsonProperty("level")]
    public string Level { get; set; }

    [JsonProperty("level_index")]
    public int LevelIdx { get; set; }

    [JsonProperty("level_label")]
    public string LevelLabel
    {
        get => MaiMaiSong.LevelNameAll[LevelIdx];
        // ReSharper disable once ValueParameterNotUsed
        set {}
    }

    [JsonProperty("ra")]
    public int Rating
    {
        get => Ra(Achievement, Constant);
        // ReSharper disable once ValueParameterNotUsed
        set {}
    }

    [JsonProperty("rate")]
    public string Rank
    {
        get => CalcRank(Achievement);
        // ReSharper disable once ValueParameterNotUsed
        set {}
    }

    [JsonProperty("id")]
    public long SongId
    {
        get => Id;
        set => Id = value;
    }

    [JsonProperty("song_id")]
    public long Id { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }

    public static DxRating FromJson(string json)
    {
        return JsonConvert.DeserializeObject<DxRating>(json, Converter.Settings)!;
    }

    public string ToJson()
    {
        return JsonConvert.SerializeObject(this, Converter.Settings);
    }

    public static int B50Ra(decimal achievement, decimal constant)
    {
        var baseRa = achievement switch
        {
            < 50     => 7.0m,
            < 60     => 8.0m,
            < 70     => 9.6m,
            < 75     => 11.2m,
            < 80     => 12.0m,
            < 90     => 13.6m,
            < 94     => 15.2m,
            < 97     => 16.8m,
            < 98     => 20.0m,
            < 99     => 20.3m,
            < 99.5m  => 20.8m,
            < 100    => 21.1m,
            < 100.5m => 21.6m,
            _        => 22.4m
        };
        return (int)Math.Floor(constant * (Math.Min(100.5m, achievement) / 100) * baseRa);
    }

    public int B50Ra()
    {
        return B50Ra((decimal)Achievement, (decimal)Constant);
    }

    public int Ra()
    {
        return Ra(Achievement, Constant);
    }

    public static int Ra(double achievement, double constant)
    {
        return B50Ra((decimal)achievement, (decimal)constant);
    }

    public static string CalcRank(double achievement)
    {
        return (achievement switch
        {
            >= 100.5 => "SSS+",
            >= 100   => "SSS",
            >= 99.5  => "SS+",
            >= 99    => "SS",
            >= 98    => "S+",
            >= 97    => "S",
            >= 94    => "AAA",
            >= 90    => "AA",
            >= 80    => "A",
            >= 75    => "BBB",
            >= 70    => "BB",
            >= 60    => "B",
            >= 50    => "C",
            _        => "D"
        }).ToLower().Replace('+', 'p');
    }

    /// <summary>
    /// 二分找下一个可以提高rating的达成率
    /// </summary>
    /// <param name="current">当前的达成率</param>
    /// <returns>达成率</returns>
    public double NextRa(double? current = null)
    {
        return NextRa(current ?? Achievement, Constant);
    }

    /// <summary>
    /// 二分找下一个可以提高rating的达成率
    /// </summary>
    /// <param name="achievement">当前的达成率</param>
    /// <param name="constant">定数</param>
    /// <returns>达成率</returns>
    public static double NextRa(double achievement, double constant)
    {
        var l = achievement;
        var r = 100.5;

        var          currentRa = Ra(l, constant);
        const double eps       = 0.00001;

        while (Math.Abs(l - r) >= eps)
        {
            var a = (l + r) / 2;
            if (Ra(a, constant) > currentRa)
            {
                r = a - eps;
            }
            else
            {
                l = a + eps;
            }
        }

        var ret = Math.Floor((r + eps) * 10000) / 10000;
        return Ra(ret, constant) > currentRa ? ret : ret + 0.0001;
    }

    /// <summary>
    /// 获取最小的达成率使得rating大于<paramref name="minRa"/>
    /// </summary>
    /// <param name="constant">定数</param>
    /// <param name="minRa">最小的Ra</param>
    /// <returns>达成率</returns>
    public static double NextAchievement(double constant, long minRa)
    {
        if (minRa >= Ra(100.5, constant)) return -1;

        var a = 0.0;

        while (a < 100.5)
        {
            a = NextRa(a, constant);

            if (Ra(a, constant) > minRa)
            {
                return a > 100.5 ? 100.5 : a;
            }
        }

        return -1;
    }
}