namespace Marisa.Plugin.Shared.MaiMaiDx;

public sealed record MaiMaiRecommendationReplacement(
    long SongId,
    string Title,
    int LevelIndex,
    double Achievement,
    int Rating);

public sealed record MaiMaiRecommendationItem(
    int Step,
    string Bucket,
    string Action,
    long SongId,
    string Title,
    string Type,
    bool IsNew,
    int LevelIndex,
    string Level,
    double Constant,
    double? CurrentAchievement,
    int BaselineRating,
    double TargetAchievement,
    int TargetRating,
    int Gain,
    RecommendationDifficulty? Difficulty,
    MaiMaiRecommendationReplacement? Replaced);

public sealed record MaiMaiRecommendationCardData(
    string Mode,
    string Nickname,
    int CurrentRating,
    int? TargetRating,
    int? ProjectedRating,
    IReadOnlyList<MaiMaiRecommendationItem> Items);

public enum MaiMaiRecommendationPlanStatus
{
    Success,
    AlreadyReached,
    Unreachable
}

public sealed record MaiMaiRecommendationPlanResult(
    MaiMaiRecommendationPlanStatus Status,
    MaiMaiRecommendationCardData? Data);

/// <summary>生成快速推分候选与目标 Rating 规划。</summary>
public sealed class MaiMaiRecommendationEngine
{
    private const int OldCapacity = 35;
    private const int NewCapacity = 15;
    private readonly IReadOnlyList<MaiMaiSong> _songs;
    private readonly Dictionary<long, MaiMaiSong> _songById;
    private readonly RecommendationDifficultyCatalog _difficultyCatalog;
    private readonly Random _random;

    public MaiMaiRecommendationEngine(
        IReadOnlyList<MaiMaiSong> songs,
        RecommendationDifficultyCatalog? difficultyCatalog = null,
        Random? random = null)
    {
        _songs             = songs;
        _songById          = songs.ToDictionary(x => x.Id);
        _difficultyCatalog = difficultyCatalog ?? RecommendationDifficultyCatalog.Default;
        _random            = random ?? Random.Shared;
    }

    public MaiMaiRecommendationCardData BuildQuick(DxRating rating)
    {
        var old = BuildInitialState(rating.OldScores);
        var @new = BuildInitialState(rating.NewScores);
        var used = old.Concat(@new).Select(x => x.Key).ToHashSet();
        var playerRating = rating.Rating;
        var items = new List<MaiMaiRecommendationItem>(4);

        AddQuick(items, BuildUpgradeCandidates(old, "old", playerRating));
        AddQuick(items, BuildEntryCandidates(old, "old", OldCapacity, false, used, playerRating));
        AddQuick(items, BuildUpgradeCandidates(@new, "new", playerRating));
        AddQuick(items, BuildEntryCandidates(@new, "new", NewCapacity, true, used, playerRating));

        return new MaiMaiRecommendationCardData(
            "quick", rating.Nickname, rating.Rating, null, null, items);
    }

    public MaiMaiRecommendationPlanResult BuildPlan(DxRating rating, int targetRating)
    {
        if (targetRating <= rating.Rating)
        {
            return new MaiMaiRecommendationPlanResult(MaiMaiRecommendationPlanStatus.AlreadyReached, null);
        }

        var old = BuildInitialState(rating.OldScores);
        var @new = BuildInitialState(rating.NewScores);
        var step = 0;

        while (Total(old, @new) < targetRating)
        {
            var used = old.Concat(@new).Select(x => x.Key).ToHashSet();
            var candidates = BuildUpgradeCandidates(old, "old", rating.Rating)
                .Concat(BuildEntryCandidates(old, "old", OldCapacity, false, used, rating.Rating))
                .Concat(BuildUpgradeCandidates(@new, "new", rating.Rating))
                .Concat(BuildEntryCandidates(@new, "new", NewCapacity, true, used, rating.Rating))
                .OrderBy(x => x.Effort)
                .ThenBy(x => x.DifficultyOrder)
                .ThenByDescending(x => x.Gain)
                .ThenBy(x => x.Song.Id)
                .ThenBy(x => x.LevelIndex)
                .ToList();

            if (candidates.Count == 0)
            {
                return new MaiMaiRecommendationPlanResult(MaiMaiRecommendationPlanStatus.Unreachable, null);
            }

            Apply(candidates[0], old, @new, ref step);
        }

        var projected = Total(old, @new);
        if (projected < targetRating)
        {
            return new MaiMaiRecommendationPlanResult(MaiMaiRecommendationPlanStatus.Unreachable, null);
        }

        var items = old.Concat(@new)
            .Where(HasChanged)
            .Select(x => ToPlanItem(x, rating.Rating))
            .OrderBy(x => x.Step)
            .ToList();

        var data = new MaiMaiRecommendationCardData(
            "plan", rating.Nickname, rating.Rating, targetRating, projected, items);
        return new MaiMaiRecommendationPlanResult(MaiMaiRecommendationPlanStatus.Success, data);
    }

    private void AddQuick(ICollection<MaiMaiRecommendationItem> items, IReadOnlyList<Candidate> candidates)
    {
        if (candidates.Count == 0) return;

        var frontier = candidates
            .Where(candidate => !candidates.Any(other => Dominates(other, candidate)))
            .OrderBy(x => x.Effort)
            .ThenBy(x => x.DifficultyOrder)
            .ThenByDescending(x => x.Gain)
            .Take(5)
            .ToList();

        var selected = frontier[_random.Next(frontier.Count)];
        items.Add(ToQuickItem(selected));
    }

    private List<ScoreState> BuildInitialState(IEnumerable<SongScore> scores)
    {
        return scores
            .Where(x => _songById.ContainsKey(x.Id))
            .Where(x => x.LevelIdx >= 0 && x.LevelIdx < _songById[x.Id].Constants.Count)
            .Select(x =>
            {
                var song = _songById[x.Id];
                var ra   = SongScore.Ra(x.Achievement, song.Constants[x.LevelIdx]);
                var origin = new SlotOrigin(song, x.LevelIdx, x.Achievement, ra);
                return new ScoreState(song, x.LevelIdx, x.Achievement, ra, origin, null);
            })
            .ToList();
    }

    private List<Candidate> BuildUpgradeCandidates(
        IReadOnlyList<ScoreState> states,
        string bucket,
        int playerRating)
    {
        return states
            .Where(x => x.Achievement < 100.5)
            .Select(x =>
            {
                var target = SongScore.NextRecommendedRa(x.Achievement, x.Constant);
                return CreateCandidate(bucket, x, x.Song, x.LevelIndex, target, x.Rating, null, playerRating);
            })
            .Where(x => x != null)
            .Cast<Candidate>()
            .ToList();
    }

    private List<Candidate> BuildEntryCandidates(
        IReadOnlyList<ScoreState> states,
        string bucket,
        int capacity,
        bool isNew,
        IReadOnlySet<(long SongId, int LevelIndex)> used,
        int playerRating)
    {
        if (states.Count == 0) return [];

        var floor = states.Count >= capacity ? states.MinBy(x => x.Rating) : null;
        var floorRating = floor?.Rating ?? 0;
        var playableMax = states.Max(x => x.Constant);
        var result = new List<Candidate>();

        foreach (var song in _songs.Where(x => x.Info.IsNew == isNew && x.Id <= 100000))
        {
            for (var levelIndex = 0; levelIndex < song.Constants.Count; levelIndex++)
            {
                var key = (song.Id, levelIndex);
                var constant = song.Constants[levelIndex];
                if (constant <= 0
                    || constant > playableMax + 0.15
                    || used.Contains(key)) continue;

                var target = SongScore.NextRecommendedAchievement(constant, floorRating);
                var candidate = CreateCandidate(
                    bucket, null, song, levelIndex, target, floorRating, floor, playerRating);
                if (candidate != null) result.Add(candidate);
            }
        }

        return result;
    }

    private Candidate? CreateCandidate(
        string bucket,
        ScoreState? current,
        MaiMaiSong song,
        int levelIndex,
        double targetAchievement,
        int baselineRating,
        ScoreState? replaced,
        int playerRating)
    {
        if (targetAchievement is < 0 or > 100.5) return null;

        var targetRating = SongScore.Ra(targetAchievement, song.Constants[levelIndex]);
        if (targetRating <= baselineRating) return null;

        _difficultyCatalog.TryEvaluate(song.Id, levelIndex, playerRating, out var difficulty);
        var effort = current == null
            ? 1 + Math.Max(0, targetAchievement - 97)
            : Math.Max(0.0001, targetAchievement - current.Achievement);

        return new Candidate(
            bucket,
            current == null ? "entry" : "upgrade",
            current,
            replaced,
            song,
            levelIndex,
            targetAchievement,
            targetRating,
            targetRating - baselineRating,
            effort,
            DifficultyOrder(song.Constants[levelIndex], difficulty),
            difficulty);
    }

    private static bool Dominates(Candidate other, Candidate candidate)
    {
        if (ReferenceEquals(other, candidate)) return false;

        const double epsilon = 0.000001;
        var noWorse = other.Effort <= candidate.Effort + epsilon
                   && other.DifficultyOrder <= candidate.DifficultyOrder + epsilon
                   && other.Gain >= candidate.Gain;
        var better = other.Effort < candidate.Effort - epsilon
                  || other.DifficultyOrder < candidate.DifficultyOrder - epsilon
                  || other.Gain > candidate.Gain;
        return noWorse && better;
    }

    private static double DifficultyOrder(double constant, RecommendationDifficulty? difficulty)
    {
        if (difficulty == null) return constant + 0.3;
        if (difficulty.Kind == "fitted_ds") return difficulty.Value;

        // 低定数数据仅提供同等级百分位；映射到半个定数宽度只用于候选排序，不作为拟合定数展示。
        return constant + (difficulty.Value - 50) * 0.005;
    }

    private static int Total(IEnumerable<ScoreState> old, IEnumerable<ScoreState> @new)
    {
        return old.Sum(x => x.Rating) + @new.Sum(x => x.Rating);
    }

    private static void Apply(
        Candidate candidate,
        IList<ScoreState> old,
        IList<ScoreState> @new,
        ref int step)
    {
        var states = candidate.Bucket == "old" ? old : @new;
        if (candidate.Current != null)
        {
            var index = states.IndexOf(candidate.Current);
            states[index] = candidate.Current with
            {
                Achievement = candidate.TargetAchievement,
                Rating      = candidate.TargetRating,
                Step        = candidate.Current.Step ?? ++step
            };
            return;
        }

        SlotOrigin? origin = null;
        int? actionStep = null;
        if (candidate.Replaced != null)
        {
            states.Remove(candidate.Replaced);
            origin     = candidate.Replaced.Origin;
            actionStep = candidate.Replaced.Step;
        }

        states.Add(new ScoreState(
            candidate.Song,
            candidate.LevelIndex,
            candidate.TargetAchievement,
            candidate.TargetRating,
            origin,
            actionStep ?? ++step));
    }

    private MaiMaiRecommendationItem ToQuickItem(Candidate candidate)
    {
        return ToItem(
            0,
            candidate.Bucket,
            candidate.Action,
            candidate.Song,
            candidate.LevelIndex,
            candidate.Current?.Achievement,
            candidate.Current?.Rating ?? candidate.Replaced?.Rating ?? 0,
            candidate.TargetAchievement,
            candidate.TargetRating,
            candidate.Difficulty,
            candidate.Replaced?.Origin);
    }

    private MaiMaiRecommendationItem ToPlanItem(ScoreState state, int playerRating)
    {
        var sameChart = state.Origin != null && state.Key == state.Origin.Key;
        _difficultyCatalog.TryEvaluate(state.Song.Id, state.LevelIndex, playerRating, out var difficulty);

        return ToItem(
            state.Step ?? 0,
            state.Song.Info.IsNew ? "new" : "old",
            sameChart ? "upgrade" : "entry",
            state.Song,
            state.LevelIndex,
            sameChart ? state.Origin!.Achievement : null,
            state.Origin?.Rating ?? 0,
            state.Achievement,
            state.Rating,
            difficulty,
            sameChart ? null : state.Origin);
    }

    private static MaiMaiRecommendationItem ToItem(
        int step,
        string bucket,
        string action,
        MaiMaiSong song,
        int levelIndex,
        double? currentAchievement,
        int baselineRating,
        double targetAchievement,
        int targetRating,
        RecommendationDifficulty? difficulty,
        SlotOrigin? replaced)
    {
        return new MaiMaiRecommendationItem(
            step,
            bucket,
            action,
            song.Id,
            song.Title,
            song.Type,
            song.Info.IsNew,
            levelIndex,
            song.Levels[levelIndex],
            song.Constants[levelIndex],
            currentAchievement,
            baselineRating,
            targetAchievement,
            targetRating,
            targetRating - baselineRating,
            difficulty,
            replaced == null
                ? null
                : new MaiMaiRecommendationReplacement(
                    replaced.Song.Id,
                    replaced.Song.Title,
                    replaced.LevelIndex,
                    replaced.Achievement,
                    replaced.Rating));
    }

    private static bool HasChanged(ScoreState state)
    {
        return state.Origin == null
            || state.Key != state.Origin.Key
            || Math.Abs(state.Achievement - state.Origin.Achievement) > 0.00001;
    }

    private sealed record SlotOrigin(
        MaiMaiSong Song,
        int LevelIndex,
        double Achievement,
        int Rating)
    {
        public (long SongId, int LevelIndex) Key => (Song.Id, LevelIndex);
    }

    private sealed record ScoreState(
        MaiMaiSong Song,
        int LevelIndex,
        double Achievement,
        int Rating,
        SlotOrigin? Origin,
        int? Step)
    {
        public (long SongId, int LevelIndex) Key => (Song.Id, LevelIndex);
        public double Constant => Song.Constants[LevelIndex];
    }

    private sealed record Candidate(
        string Bucket,
        string Action,
        ScoreState? Current,
        ScoreState? Replaced,
        MaiMaiSong Song,
        int LevelIndex,
        double TargetAchievement,
        int TargetRating,
        int Gain,
        double Effort,
        double DifficultyOrder,
        RecommendationDifficulty? Difficulty);
}
