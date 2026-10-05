namespace Marisa.Plugin.Shared.MaiMaiDx;

/// <summary>群内多人对战房间。消息会并发进入，状态改动都在锁内完成。</summary>
public sealed class MaiVersusRoom
{
    public const int MaxPlayers = 8;
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ScoreCacheTtl = TimeSpan.FromSeconds(60);

    private readonly object _gate = new();
    private readonly List<long> _members;
    private readonly HashSet<(long Id, int LevelIdx)> _compared = [];
    private readonly List<(long Qq, string Name, double Wins, int Matchups, int Rounds)> _standings = [];
    private readonly Dictionary<long, (DateTimeOffset At, MaiVersusMulti.Player Player)> _scores = [];
    private readonly TimeProvider _time;
    private DateTimeOffset _lastActive;
    private bool _running;
    private bool _closed;

    public MaiVersusRoom(long host, IEnumerable<long> invitees, TimeProvider? time = null)
    {
        _time       = time ?? TimeProvider.System;
        _members    = invitees.Prepend(host).Distinct().ToList();
        _lastActive = _time.GetUtcNow();
        Host        = host;

        if (_members.Count > MaxPlayers) throw new ArgumentOutOfRangeException(nameof(invitees));
    }

    public long Host { get; private set; }

    /// <summary>已完成的轮数。</summary>
    public int Round { get; private set; }

    public int Count
    {
        get
        {
            lock (_gate) return _members.Count;
        }
    }

    public bool IsExpired
    {
        get
        {
            lock (_gate) return _closed || !_running && _time.GetUtcNow() - _lastActive > IdleTimeout;
        }
    }

    public JoinResult Join(long qq)
    {
        lock (_gate)
        {
            if (_closed) return JoinResult.Closed;
            if (_members.Contains(qq)) return JoinResult.AlreadyIn;
            if (_members.Count >= MaxPlayers) return JoinResult.Full;

            _members.Add(qq);
            Touch();
            return JoinResult.Joined;
        }
    }

    public LeaveResult Leave(long qq)
    {
        lock (_gate)
        {
            if (_closed || !_members.Remove(qq)) return LeaveResult.NotIn;

            Touch();
            if (_members.Count == 0)
            {
                _closed = true;
                return LeaveResult.Dissolved;
            }

            if (qq != Host) return LeaveResult.Left;

            Host = _members[0];
            return LeaveResult.HostChanged;
        }
    }

    /// <summary>第一轮只有房主能开始，之后房间里任何人都能开下一轮。</summary>
    public StartResult TryStart(long qq, out IReadOnlyList<long> roster)
    {
        lock (_gate)
        {
            roster = [];
            if (_closed || !_members.Contains(qq)) return StartResult.NotMember;
            if (_running) return StartResult.Running;
            if (Round == 0 && qq != Host) return StartResult.NotHost;
            if (_members.Count < 2) return StartResult.TooFew;

            _running = true;
            roster   = _members.ToArray();
            return StartResult.Started;
        }
    }

    /// <summary>结束本轮；<paramref name="result" /> 为空表示本轮没有比成。</summary>
    public void EndRound(MaiVersusMulti? result)
    {
        lock (_gate)
        {
            _running = false;
            Touch();
            if (result is null) return;

            Round++;
            for (var i = 0; i < result.Players.Count; i++)
            {
                var player = result.Players[i];
                var total  = result.Totals[i];
                var index  = _standings.FindIndex(x => x.Qq == player.Qq);
                var (wins, matchups, rounds) = index < 0 ? (0.0, 0, 0) : (_standings[index].Wins, _standings[index].Matchups, _standings[index].Rounds);
                var entry  = (player.Qq, player.Name, wins + total.Points, matchups + total.Matchups, rounds + 1);

                if (index < 0) _standings.Add(entry);
                else _standings[index] = entry;
            }
        }
    }

    public void Close()
    {
        lock (_gate) _closed = true;
    }

    /// <summary>按对位胜率排序；同胜率时对位多的在前，退出的人保留在榜上。</summary>
    public IReadOnlyList<MaiVersusMulti.Standing> Standings()
    {
        lock (_gate)
        {
            return _standings
                .Select(x => new MaiVersusMulti.Standing(x.Name, x.Wins, x.Matchups, x.Rounds, !_members.Contains(x.Qq)))
                .OrderByDescending(x => x.Rate ?? -1)
                .ThenByDescending(x => x.Matchups)
                .ToArray();
        }
    }

    /// <summary>本房间比过的谱面不再抽到；都比过一遍后重新开始。</summary>
    public List<(double Constant, int LevelIdx, MaiMaiSong Song)> PickCharts(
        IReadOnlyList<(double Constant, int LevelIdx, MaiMaiSong Song)> charts,
        IReadOnlyList<MaiVersusMulti.Player> players,
        int count,
        Random random)
    {
        var scores = players.Select(p => p.Scores).ToArray();

        lock (_gate)
        {
            var picked = MaiVersusMulti.PickCharts(charts.Where(x => !_compared.Contains((x.Song.Id, x.LevelIdx))), scores, count, random);
            if (picked.Count == 0 && _compared.Count > 0)
            {
                _compared.Clear();
                picked = MaiVersusMulti.PickCharts(charts, scores, count, random);
            }

            _compared.UnionWith(picked.Select(x => (x.Song.Id, x.LevelIdx)));
            return picked;
        }
    }

    public bool TryGetScores(long qq, out MaiVersusMulti.Player player)
    {
        lock (_gate)
        {
            if (_scores.TryGetValue(qq, out var cached) && _time.GetUtcNow() - cached.At <= ScoreCacheTtl)
            {
                player = cached.Player;
                return true;
            }

            player = null!;
            return false;
        }
    }

    public void CacheScores(MaiVersusMulti.Player player)
    {
        lock (_gate) _scores[player.Qq] = (_time.GetUtcNow(), player);
    }

    private void Touch() => _lastActive = _time.GetUtcNow();

    public enum JoinResult { Joined, AlreadyIn, Full, Closed }

    public enum LeaveResult { NotIn, Left, HostChanged, Dissolved }

    public enum StartResult { Started, NotMember, NotHost, Running, TooFew }
}
