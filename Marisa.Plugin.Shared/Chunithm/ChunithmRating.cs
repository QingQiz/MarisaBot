using Newtonsoft.Json;

namespace Marisa.Plugin.Shared.Chunithm;

#pragma warning disable CS8618

public class ChunithmRating
{
    [JsonProperty("rating", Required = Required.Always)]
    public decimal Rating
    {
        get
        {
            var (r10, b30) = (Records.Best.Sum(s => s.Rating), Records.R10.Sum(s => s.Rating));
            return Math.Round((r10 + b30) / 40, 2, MidpointRounding.ToZero);
        }
        // ReSharper disable once ValueParameterNotUsed
        set {}
    }

    [JsonProperty("records", Required = Required.Always)]
    public Records Records { get; set; }

    [JsonProperty("nickname")]
    public string Username { get; set; }

    public decimal B30 => Math.Round(Records.Best.Sum(s => s.Rating) / 30, 2, MidpointRounding.ToZero);
    public decimal R10 => Math.Round(Records.R10.Sum(s => s.Rating) / 10, 2, MidpointRounding.ToZero);
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
    public ChunithmScore[] R10 { get; set; }
}