#pragma warning disable CS8618

namespace Marisa.Plugin.Shared.Configuration;

public class TodayFortune
{
    private string[]? _rhythmGames;
    private string[]? _direction;
    private string[]? _position;
    private TodayFortuneEvent[]? _events;

    public string[] RhythmGames
    {
        get => ConfigurationManager.RequireArray("fortune.rhythmGames", _rhythmGames);
        set => _rhythmGames = value;
    }

    internal string[]? RhythmGamesRaw => _rhythmGames;

    public string[] Direction
    {
        get => ConfigurationManager.RequireArray("fortune.direction", _direction);
        set => _direction = value;
    }

    internal string[]? DirectionRaw => _direction;

    public string[] Position
    {
        get => ConfigurationManager.RequireArray("fortune.position", _position);
        set => _position = value;
    }

    internal string[]? PositionRaw => _position;

    public TodayFortuneEvent[] Events
    {
        get => ConfigurationManager.RequireArray("fortune.events", _events);
        set => _events = value;
    }

    internal TodayFortuneEvent[]? EventsRaw => _events;
}
