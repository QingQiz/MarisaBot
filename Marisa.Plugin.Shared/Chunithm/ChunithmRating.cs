using Newtonsoft.Json;

namespace Marisa.Plugin.Shared.Chunithm;

#pragma warning disable CS8618

public class ChunithmRating
{
    public bool IsB50;
    public string DataSource;

    [JsonProperty("rating", Required = Required.Always)]
    public decimal Rating
    {
        get
        {
            var r = Records.Best.Sum(s => s.Rating);
            var b = Records.Recent.Sum(s => s.Rating);
            return Math.Round((r + b) / (IsB50 ? 50 : 40), 2, MidpointRounding.ToZero);
        }
        // ReSharper disable once ValueParameterNotUsed
        set {}
    }

    [JsonProperty("records", Required = Required.Always)]
    public Records Records { get; set; }

    [JsonProperty("nickname")]
    public string Username { get; set; }

    public decimal B30 => Math.Round(Records.Best.Sum(s => s.Rating) / 30, 2, MidpointRounding.ToZero);
    public decimal R10 => Math.Round(Records.Recent.Take(10).Sum(s => s.Rating) / 10, 2, MidpointRounding.ToZero);
    public decimal R20 => Math.Round(Records.Recent.Take(20).Sum(s => s.Rating) / 20, 2, MidpointRounding.ToZero);
}

public class Records
{
    private ChunithmScore[]? _best;

    [JsonProperty("b30")]
    public ChunithmScore[] B30
    {
        get => _best ?? [];
        set => _best = value;
    }

    [JsonProperty("best")]
    public ChunithmScore[] Best
    {
        get => _best ?? B30;
        set => _best = value;
    }

    [JsonProperty("r10", Required = Required.Always)]
    public ChunithmScore[] Recent { get; set; }
}