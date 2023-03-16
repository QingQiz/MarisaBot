using Marisa.Plugin.Shared.Osu.Entity.Score;
using Marisa.Plugin.Shared.Osu.Entity.User;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Taiko;
using osu.Game.Scoring;
using osu.Game.Skinning;
using osu.Game.Utils;
using Beatmap = osu.Game.Beatmaps.Beatmap;

namespace Marisa.Plugin.Shared.Osu;

public static class PerformanceCalculator
{
    private static Mod[] GetMods(Ruleset ruleset, string[]? modsIn)
    {
        if (modsIn == null) return Array.Empty<Mod>();

        var availableMods = ruleset.CreateAllMods().ToList();
        var mods          = new List<Mod>();

        foreach (var modString in modsIn)
        {
            var newMod = availableMods
                .FirstOrDefault(m => string.Equals(m.Acronym, modString, StringComparison.CurrentCultureIgnoreCase));

            if (newMod == null) throw new ArgumentException($"Invalid mod provided: {modString}");

            mods.Add(newMod);
        }

        return mods.ToArray();
    }

    private static Ruleset GetRuleset(int modeInt)
    {
        return modeInt switch
        {
            0 => new OsuRuleset(),
            1 => new TaikoRuleset(),
            2 => new CatchRuleset(),
            3 => new ManiaRuleset(),
            _ => throw new ArgumentOutOfRangeException(nameof(modeInt), modeInt, null)
        };
    }

    public static double StarRating(this OsuScore score)
    {
        var starRatingChangeMods = new[] { "ez", "hr", "fl", "dt", "ht", "nc" };
        var ruleSetChangeMods    = Enumerable.Range(1, 12).Select(i => $"{i}k").ToArray();

        var starRatingChanged = starRatingChangeMods.Any(m1 => score.Mods.Any(m2 => m1.Equals(m2, StringComparison.OrdinalIgnoreCase)));
        var ruleSetChanged    = ruleSetChangeMods.Any(m1 => score.Mods.Any(m2 => m1.Equals(m2, StringComparison.OrdinalIgnoreCase)));

        if (!starRatingChanged && !ruleSetChanged)
        {
            return score.Beatmap.StarRating;
        }

        string path;
        try
        {
            path = OsuApi.GetBeatmapPath(score.Beatmap);
        }
        catch (Exception e) when (e is FileNotFoundException or HttpRequestException)
        {
            return score.Beatmap.StarRating;
        }

        var ruleset = GetRuleset(score.ModeInt);

        var mods           = LegacyHelper.ConvertToLegacyDifficultyAdjustmentMods(ruleset, GetMods(ruleset, score.Mods));
        var workingBeatmap = ProcessorWorkingBeatmap.FromFile(path);
        var attributes     = ruleset.CreateDifficultyCalculator(workingBeatmap).Calculate(mods);

        return attributes.StarRating;
    }

    public static double PerformancePoint(this OsuScore score)
    {
        if (score.Pp != null)
        {
            return (double)score.Pp;
        }

        string path;

        try
        {
            path = OsuApi.GetBeatmapPath(score.Beatmap);
        }
        catch (Exception e) when (e is FileNotFoundException or HttpRequestException)
        {
            return 0;
        }

        var ruleset = GetRuleset(score.ModeInt);

        var mods           = LegacyHelper.ConvertToLegacyDifficultyAdjustmentMods(ruleset, GetMods(ruleset, score.Mods));
        var workingBeatmap = ProcessorWorkingBeatmap.FromFile(path);
        var beatmap        = workingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo, mods);

        var difficultyCalculator  = ruleset.CreateDifficultyCalculator(workingBeatmap);
        var difficultyAttributes  = difficultyCalculator.Calculate(mods);
        var performanceCalculator = ruleset.CreatePerformanceCalculator();

        var ppAttributes = performanceCalculator!.Calculate(new ScoreInfo(beatmap.BeatmapInfo, ruleset.RulesetInfo)
        {
            Accuracy = score.Accuracy,
            MaxCombo = score.MaxCombo,
            Statistics = new Dictionary<HitResult, int>
            {
                { HitResult.Perfect, score.Statistics.Count300P },
                { HitResult.Great, score.Statistics.Count300 },
                { HitResult.Good, score.Statistics.Count200 },
                { HitResult.Ok, score.Statistics.Count100 },
                { HitResult.Meh, score.Statistics.Count50 },
                { HitResult.Miss, score.Statistics.CountMiss }
            },
            Mods       = mods,
            TotalScore = score.Score,
        }, difficultyAttributes);

        return ppAttributes!.Total;
    }

    public static (double scorePp, double bonusPp, long rankedScores) BonusPp(this OsuUserInfo info, IEnumerable<OsuScore> scores)
    {
        var scorePp = scores.Sum(s => s.Weight!.Pp);
        var bonusPp = info.Statistics.Pp - scorePp;

        var totalScores =
            info.Statistics.GradeCounts["a"] +
            info.Statistics.GradeCounts["s"] +
            info.Statistics.GradeCounts["sh"] +
            info.Statistics.GradeCounts["ss"] +
            info.Statistics.GradeCounts["ssh"];

        if (!double.IsNaN(scorePp) && !double.IsNaN(bonusPp))
        {
            return (scorePp, bonusPp, totalScores);
        }

        return (0, 0, 0);
    }
}

internal class ProcessorWorkingBeatmap : WorkingBeatmap
{
    private readonly Beatmap _beatmap;

    /// <summary>
    /// Constructs a new <see cref="ProcessorWorkingBeatmap"/> from a .osu file.
    /// </summary>
    /// <param name="file">The .osu file.</param>
    /// <param name="beatmapId">An optional beatmap ID (for cases where .osu file doesn't have one).</param>
    private ProcessorWorkingBeatmap(string file, int? beatmapId = null)
        : this(ReadFromFile(file), beatmapId)
    {
    }

    private ProcessorWorkingBeatmap(Beatmap beatmap, int? beatmapId = null)
        : base(beatmap.BeatmapInfo, null)
    {
        _beatmap                    = beatmap;
        beatmap.BeatmapInfo.Ruleset = LegacyHelper.GetRulesetFromLegacyId(beatmap.BeatmapInfo.Ruleset.OnlineID).RulesetInfo;

        if (beatmapId.HasValue) beatmap.BeatmapInfo.OnlineID = beatmapId.Value;
    }

    private static Beatmap ReadFromFile(string filename)
    {
        using var stream = File.OpenRead(filename);
        using var reader = new LineBufferedReader(stream);
        return Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
    }

    public static ProcessorWorkingBeatmap FromFile(string file)
    {
        if (!File.Exists(file)) throw new ArgumentException($"Beatmap file {file} does not exist.");

        return new ProcessorWorkingBeatmap(file);
    }

    protected override IBeatmap GetBeatmap() => _beatmap;

    protected override Texture GetBackground()
    {
        throw new NotImplementedException();
    }

    protected override Track GetBeatmapTrack()
    {
        throw new NotImplementedException();
    }

    protected override ISkin GetSkin()
    {
        throw new NotImplementedException();
    }

    public override Stream GetStream(string storagePath)
    {
        throw new NotImplementedException();
    }
}

internal static class LegacyHelper
{
    public static Ruleset GetRulesetFromLegacyId(int id)
    {
        return id switch
        {
            0 => new OsuRuleset(),
            1 => new TaikoRuleset(),
            2 => new CatchRuleset(),
            3 => new ManiaRuleset(),
            _ => throw new ArgumentException("Invalid ruleset ID provided.")
        };
    }

    /// <summary>
    /// Transforms a given <see cref="Mod"/> combination into one which is applicable to legacy scores.
    /// This is used to match osu!stable/osu!web calculations for the time being, until such a point that these mods do get considered.
    /// </summary>
    public static Mod[] ConvertToLegacyDifficultyAdjustmentMods(Ruleset ruleset, Mod[] mods)
    {
        var beatmap = new EmptyWorkingBeatmap
        {
            BeatmapInfo =
            {
                Ruleset    = ruleset.RulesetInfo,
                Difficulty = new BeatmapDifficulty()
            }
        };

        var allMods = ruleset.CreateAllMods().ToArray();

        var allowedMods = ModUtils.FlattenMods(
                ruleset.CreateDifficultyCalculator(beatmap).CreateDifficultyAdjustmentModCombinations())
            .Select(m => m.GetType())
            .Distinct()
            .ToHashSet();

        // Special case to allow either DT or NC.
        if (mods.Any(m => m is ModDoubleTime)) allowedMods.Add(allMods.Single(m => m is ModNightcore).GetType());

        var result = new List<Mod>();

        var classicMod = allMods.SingleOrDefault(m => m is ModClassic);
        if (classicMod != null) result.Add(classicMod);

        result.AddRange(mods.Where(m => allowedMods.Contains(m.GetType())));

        return result.ToArray();
    }

    private class EmptyWorkingBeatmap : WorkingBeatmap
    {
        public EmptyWorkingBeatmap()
            : base(new BeatmapInfo(), null)
        {
        }

        protected override IBeatmap GetBeatmap() => throw new NotImplementedException();

        protected override Texture GetBackground() => throw new NotImplementedException();

        protected override Track GetBeatmapTrack() => throw new NotImplementedException();

        protected override ISkin GetSkin() => throw new NotImplementedException();

        public override Stream GetStream(string storagePath) => throw new NotImplementedException();
    }
}