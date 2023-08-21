namespace Marisa.Plugin.Shared.MaiMaiDx;

public class DxRating
{
    public readonly List<SongScore> NewScores = new();
    public readonly List<SongScore> OldScores = new();

    public DxRating(dynamic data)
    {
        foreach (var d in data.charts.dx) NewScores.Add(new SongScore(d));
        foreach (var d in data.charts.sd) OldScores.Add(new SongScore(d));

        NewScores.ForEach(s => s.Rating = s.B50Ra());
        OldScores.ForEach(s => s.Rating = s.B50Ra());

        NewScores = NewScores.OrderByDescending(s => s.Rating).ToList();
        OldScores = OldScores.OrderByDescending(s => s.Rating).ToList();
    }
}