namespace Marisa.Plugin.Shared.MaiMaiDx;

public class DxRating
{
    public readonly long AdditionalRating;
    public readonly List<SongScore> NewScores = new();
    public readonly List<SongScore> OldScores = new();
    public readonly string Nickname;

    public DxRating(dynamic data)
    {
        AdditionalRating = data.additional_rating;
        Nickname         = data.nickname;

        foreach (var d in data.charts.dx) NewScores.Add(new SongScore(d));
        foreach (var d in data.charts.sd) OldScores.Add(new SongScore(d));

        NewScores.ForEach(s => s.Rating = s.B50Ra());
        OldScores.ForEach(s => s.Rating = s.B50Ra());

        NewScores = NewScores.OrderByDescending(s => s.Rating).ToList();
        OldScores = OldScores.OrderByDescending(s => s.Rating).ToList();
    }
}