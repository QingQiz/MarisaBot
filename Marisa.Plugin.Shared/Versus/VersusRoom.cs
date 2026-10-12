namespace Marisa.Plugin.Shared.Versus;

/// <summary>群内对战房间；消息可并发进入，状态改动均在锁内完成。</summary>
public sealed class VersusRoom<TScore>
{
    public const int MaxPlayers = 8;
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ScoreCacheTtl = TimeSpan.FromSeconds(60);

    private readonly object _gate = new();
    private readonly List<long> _members;
    private readonly HashSet<(long Id, int LevelIdx)> _compared = [];
    private readonly List<(long Qq, string Name, double Wins, int Matchups, int Rounds)> _standings = [];
    private readonly Dictionary<long, (DateTimeOffset At, VersusPlayer<TScore> Player)> _scores = [];
    private readonly TimeProvider _time;
    private DateTimeOffset _lastActive;
    private bool _running;
    private bool _closed;

    public VersusRoom(long host, IEnumerable<long> invitees, TimeProvider? time = null)
    {
        _time = time ?? TimeProvider.System;
        _members = invitees.Prepend(host).Distinct().ToList();
        _lastActive = _time.GetUtcNow();
        Host = host;
        if (_members.Count > MaxPlayers) throw new ArgumentOutOfRangeException(nameof(invitees));
    }

    public long Host { get; private set; }
    public int Round { get; private set; }

    public int Count
    {
        get { lock (_gate) return _members.Count; }
    }

    public bool IsExpired
    {
        get { lock (_gate) return _closed || !_running && _time.GetUtcNow() - _lastActive > IdleTimeout; }
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

    /// <summary>首个成功轮次仅房主能开始，之后所有成员都能开下一轮。</summary>
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
            roster = _members.ToArray();
            return StartResult.Started;
        }
    }

    /// <summary>空结果不计轮数；返回本轮结束时的固定快照供异步绘图使用。</summary>
    public VersusRoomSnapshot EndRound(IReadOnlyList<VersusRoundPlayer>? result)
    {
        lock (_gate)
        {
            _running = false;
            Touch();
            if (result is not null)
            {
                Round++;
                foreach (var player in result)
                {
                    var index = _standings.FindIndex(x => x.Qq == player.Qq);
                    var (wins, matchups, rounds) = index < 0 ? (0.0, 0, 0) : (_standings[index].Wins, _standings[index].Matchups, _standings[index].Rounds);
                    var entry = (player.Qq, player.Name, wins + player.Points, matchups + player.Matchups, rounds + 1);
                    if (index < 0) _standings.Add(entry);
                    else _standings[index] = entry;
                }
            }

            return new VersusRoomSnapshot(Round, Standings());
        }
    }

    public void Close()
    {
        lock (_gate) _closed = true;
    }

    public IReadOnlyList<VersusStanding> Standings()
    {
        lock (_gate)
        {
            return _standings
                .Select(x => new VersusStanding(x.Name, x.Wins, x.Matchups, x.Rounds, !_members.Contains(x.Qq)))
                .OrderByDescending(x => x.Rate ?? -1)
                .ThenByDescending(x => x.Matchups)
                .ToArray();
        }
    }

    /// <summary>当前成员没有剩余可抽谱面时重新开始一轮覆盖周期。</summary>
    public List<TChart> PickCharts<TChart>(
        IReadOnlyList<TChart> charts,
        IReadOnlyList<VersusPlayer<TScore>> players,
        Func<TChart, (long Id, int LevelIdx)> keySelector,
        Func<TChart, double> constantSelector,
        int count,
        Random random)
    {
        var scores = players.Select(p => p.Scores).ToArray();
        lock (_gate)
        {
            var picked = VersusSampler.Pick(charts.Where(x => !_compared.Contains(keySelector(x))), scores, keySelector, constantSelector, count, random);
            if (picked.Count == 0 && _compared.Count > 0)
            {
                _compared.Clear();
                picked = VersusSampler.Pick(charts, scores, keySelector, constantSelector, count, random);
            }

            _compared.UnionWith(picked.Select(keySelector));
            return picked;
        }
    }

    public bool TryGetScores(long qq, out VersusPlayer<TScore> player)
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

    public void CacheScores(VersusPlayer<TScore> player)
    {
        lock (_gate) _scores[player.Qq] = (_time.GetUtcNow(), player);
    }

    private void Touch() => _lastActive = _time.GetUtcNow();

    public enum JoinResult { Joined, AlreadyIn, Full, Closed }
    public enum LeaveResult { NotIn, Left, HostChanged, Dissolved }
    public enum StartResult { Started, NotMember, NotHost, Running, TooFew }
}
