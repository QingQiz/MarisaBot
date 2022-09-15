namespace Marisa.Plugin.Shared.MaiMaiDx;

public class DxRating
{
    public readonly long AdditionalRating;
    public readonly List<SongScore> NewScores = new();
    public readonly List<SongScore> OldScores = new();
    public readonly string Nickname;
    public readonly bool B50;

    public DxRating(dynamic data, bool b50)
    {
        AdditionalRating = data.additional_rating;
        Nickname         = data.nickname;
        B50              = b50;
        foreach (var d in data.charts.dx) NewScores.Add(new SongScore(d));

        foreach (var d in data.charts.sd) OldScores.Add(new SongScore(d));

        if (B50)
        {
            NewScores.ForEach(s => s.Rating = s.B50Ra());
            OldScores.ForEach(s => s.Rating = s.B50Ra());
        }

        NewScores = NewScores.OrderByDescending(s => s.Rating).ToList();
        OldScores = OldScores.OrderByDescending(s => s.Rating).ToList();
    }
}