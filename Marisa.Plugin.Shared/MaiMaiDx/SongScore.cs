namespace Marisa.Plugin.Shared.MaiMaiDx;

public class SongScore
{
    public readonly double Achievement;
    public readonly double Constant;
    public long DxScore;
    public readonly string Fc;
    public readonly string Fs;
    public string Level;
    public readonly long LevelIdx;
    public string LevelLabel;
    public long Rating;
    public readonly string Rank;
    public readonly long Id;
    public readonly string Title;
    public readonly string Type;

    public SongScore(dynamic data)
    {
        Achievement = data.achievements;
        Constant    = data.ds;
        DxScore     = data.dxScore;
        Fc          = data.fc;
        Fs          = data.fs;
        Level       = data.level;
        LevelIdx    = data.level_index;
        LevelLabel  = data.level_label;
        Rating      = data.ra;
        Rank        = data.rate;
        Id          = data.song_id;
        Title       = data.title;
        Type        = data.type;
    }

    public SongScore(
        double achievement, double constant, long dxScore, string fc, string fs, string level,
        long levelIdx, string levelLabel, long rating, string rank, long id, string title, string type)
    {
        Achievement = achievement;
        Constant    = constant;
        DxScore     = dxScore;
        Fc          = fc;
        Fs          = fs;
        Level       = level;
        LevelIdx    = levelIdx;
        LevelLabel  = levelLabel;
        Rating      = rating;
        Rank        = rank;
        Id          = id;
        Title       = title;
        Type        = type;
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
        var baseRa = achievement switch
        {
            < 50    => 0,
            < 60    => 5,
            < 70    => 6,
            < 75    => 7,
            < 80    => 7.5,
            < 90    => 8.5,
            < 94    => 9.5,
            < 97    => 10.5,
            < 98    => 12.5,
            < 99    => 12.7,
            < 99.5  => 13,
            < 100   => 13.2,
            < 100.5 => 13.5,
            _       => 14.0
        };
        return (int)Math.Floor(constant * (Math.Min(100.5, achievement) / 100) * baseRa);
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
    public static double NextRa(double constant, long minRa)
    {
        var a = 0.0;

        while (a < 100.5)
        {
            a = NextRa(a, constant);

            if (Ra(a, constant) > minRa)
            {
                return a;
            }
        }

        return -1;
    }
}