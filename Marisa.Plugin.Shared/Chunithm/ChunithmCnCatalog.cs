using System.Collections.Concurrent;
using System.Dynamic;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Flurl.Http;
using Marisa.Configuration;

namespace Marisa.Plugin.Shared.Chunithm;

/// <summary>独立于查歌本地资料的国服对战曲库，保留最近一次完整、有效的公开曲库。</summary>
public sealed class ChunithmCnCatalog
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);
    private static readonly TimeSpan FailureRetry = TimeSpan.FromMinutes(5);
    private readonly Func<Task<string>> _fetch;
    private readonly Func<string> _cachePath;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, CacheState> _states = new(StringComparer.OrdinalIgnoreCase);

    public static ChunithmCnCatalog Shared { get; } = new(
        () => "https://maimai.lxns.net/api/v0/chunithm/song/list?notes=true".WithTimeout(30).GetStringAsync(),
        () => Path.Join(ConfigurationManager.Configuration.Chunithm.TempPath, "Versus.CnCatalog.lxns.json"),
        TimeProvider.System);

    public ChunithmCnCatalog(Func<Task<string>> fetch, string cachePath, TimeProvider? timeProvider = null)
        : this(fetch, () => cachePath, timeProvider ?? TimeProvider.System)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cachePath);
    }

    private ChunithmCnCatalog(Func<Task<string>> fetch, Func<string> cachePath, TimeProvider timeProvider)
    {
        _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));
        _cachePath = cachePath;
        _timeProvider = timeProvider;
    }

    /// <summary>只请求下次刷新，不删除已成功的内存或磁盘快照。</summary>
    public void Invalidate()
    {
        var state = _states.GetOrAdd(Path.GetFullPath(_cachePath()), _ => new CacheState());
        Interlocked.Increment(ref state.Generation);
    }

    public async Task<IReadOnlyList<ChunithmSong>> GetAsync()
    {
        // Shared 会读取当前配置路径；测试或运行时换配置不能复用别的实例目录的快照。
        var path = Path.GetFullPath(_cachePath());
        var state = _states.GetOrAdd(path, _ => new CacheState());
        await state.Gate.WaitAsync();
        try
        {
            var now = _timeProvider.GetUtcNow();
            var generation = Volatile.Read(ref state.Generation);
            if (!state.DiskLoaded)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        state.Songs = Parse(await File.ReadAllTextAsync(path));
                        state.NextAttempt = new DateTimeOffset(File.GetLastWriteTimeUtc(path)) + Lifetime;
                    }
                    catch (InvalidDataException)
                    {
                        // 只有本类的独立文件参与恢复；损坏的数据不获得国服谱面资格。
                    }
                }
                state.DiskLoaded = true;
            }

            if (generation == state.LoadedGeneration && now < state.NextAttempt)
                return state.Songs ?? throw Unavailable(state.LastFailure);

            try
            {
                var json = await _fetch();
                var songs = Parse(json);
                await SaveSnapshot(path, json);
                state.Songs = songs;
                state.NextAttempt = _timeProvider.GetUtcNow() + Lifetime;
                state.LastFailure = null;
            }
            catch (Exception exception) when (exception is HttpRequestException or FlurlHttpException or InvalidDataException)
            {
                state.NextAttempt = _timeProvider.GetUtcNow() + FailureRetry;
                state.LastFailure = exception;
            }
            state.LoadedGeneration = generation;
            return state.Songs ?? throw Unavailable(state.LastFailure);
        }
        finally
        {
            state.Gate.Release();
        }
    }

    /// <summary>按落雪 difficulty 字段建立 BASIC～ULTIMA，不根据数组位置或本地日服曲库推断。</summary>
    public static IReadOnlyList<ChunithmSong> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw Invalid("响应为空");
        try
        {
            using var document = JsonDocument.Parse(json);
            EnsureUniqueProperties(document.RootElement);
            var root = document.RootElement;
            var versions = new Dictionary<int, string>();
            foreach (var entry in Array(root, "versions"))
            {
                var version = Integer(entry, "version");
                if (!versions.TryAdd(version, String(entry, "title", required: true)))
                    throw Invalid("重复的版本编号");
            }
            if (versions.Count == 0) throw Invalid("版本列表为空");

            var songs = new List<ChunithmSong>();
            var ids = new HashSet<long>();
            foreach (var item in Array(root, "songs"))
            {
                var id = item.GetProperty("id").GetInt64();
                if (id <= 0 || !ids.Add(id)) throw Invalid("无效或重复的歌曲编号");
                var title = String(item, "title", required: true);
                var artist = String(item, "artist");
                var genre = String(item, "genre");
                if (!versions.TryGetValue(Integer(item, "version"), out var version))
                    throw Invalid("歌曲版本不在版本列表中");
                var bpm = Number(item, "bpm");
                if (bpm <= 0) throw Invalid("无效的 BPM");

                var difficulties = new HashSet<int>();
                var beatmaps = new SortedDictionary<int, object>();
                foreach (var chart in Array(item, "difficulties"))
                {
                    var difficulty = Integer(chart, "difficulty");
                    if (difficulty is < 0 or > 5 || !difficulties.Add(difficulty))
                        throw Invalid("无效或重复的谱面难度");
                    if (difficulty == 5) continue;

                    var level = String(chart, "level", required: true);
                    if (!Regex.IsMatch(level, @"^(?:[1-9]|1[0-6])\+?$")) throw Invalid("无效的显示等级");
                    var constant = Number(chart, "level_value");
                    if (constant is < 1 or > 16) throw Invalid("无效的谱面定数");
                    var chartBpm = chart.TryGetProperty("bpm", out var chartBpmValue) ? chartBpmValue.GetDouble() : bpm;
                    if (!double.IsFinite(chartBpm) || chartBpm <= 0) throw Invalid("无效的谱面 BPM");
                    long combo = 0;
                    if (chart.TryGetProperty("notes", out var notes) && notes.ValueKind != JsonValueKind.Null)
                    {
                        combo = notes.GetProperty("total").GetInt64();
                        if (combo < 0) throw Invalid("无效的总 Combo");
                    }

                    dynamic beatmap = new ExpandoObject();
                    beatmap.LevelName = ChunithmSong.LevelLabel[difficulty];
                    beatmap.LevelStr = level;
                    beatmap.Constant = constant;
                    beatmap.Charter = String(chart, "note_designer");
                    beatmap.ChartName = "";
                    beatmap.MaxCombo = combo;
                    beatmap.Bpm = chartBpm.ToString(CultureInfo.InvariantCulture);
                    beatmaps.Add(difficulty, beatmap);
                }
                if (beatmaps.Count == 0) continue;
                dynamic song = new ExpandoObject();
                song.Id = id;
                song.Title = title;
                song.Artist = artist;
                song.Genre = genre;
                song.Version = version;
                song.Beatmaps = beatmaps.Values.ToArray();
                var parsed = new ChunithmSong(song, ChunithmSong.DataSource.Default) { Bpm = bpm };
                songs.Add(parsed);
            }
            if (songs.Count == 0) throw Invalid("国服标准谱面列表为空");
            return songs.AsReadOnly();
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new InvalidDataException("落雪国服曲库格式无效", exception);
        }
    }

    private static JsonElement.ArrayEnumerator Array(JsonElement item, string property) => item.GetProperty(property).EnumerateArray();
    private static int Integer(JsonElement item, string property) => item.GetProperty(property).GetInt32();
    private static string String(JsonElement item, string property, bool required = false)
    {
        var value = item.GetProperty(property).GetString() ?? "";
        if (required && string.IsNullOrWhiteSpace(value)) throw Invalid($"{property} 为空");
        return value;
    }
    private static double Number(JsonElement item, string property)
    {
        var value = item.GetProperty(property).GetDouble();
        return double.IsFinite(value) ? value : throw Invalid($"{property} 非有限数值");
    }
    private static InvalidDataException Invalid(string reason) => new("落雪国服曲库格式无效：" + reason);
    private static HttpRequestException Unavailable(Exception? cause) => new("国服曲库暂不可用，请稍后重试", cause);

    private static void EnsureUniqueProperties(JsonElement item)
    {
        if (item.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in item.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw Invalid("重复的 JSON 字段");
                EnsureUniqueProperties(property.Value);
            }
        }
        else if (item.ValueKind == JsonValueKind.Array)
            foreach (var child in item.EnumerateArray()) EnsureUniqueProperties(child);
    }

    private static async Task SaveSnapshot(string path, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var staging = path + "." + Guid.NewGuid().ToString("N") + ".writing";
        try
        {
            await File.WriteAllTextAsync(staging, json);
            File.Move(staging, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(staging)) File.Delete(staging);
        }
    }

    private sealed class CacheState
    {
        public readonly SemaphoreSlim Gate = new(1, 1);
        public IReadOnlyList<ChunithmSong>? Songs;
        public bool DiskLoaded;
        public DateTimeOffset NextAttempt;
        public int Generation;
        public int LoadedGeneration;
        public Exception? LastFailure;
    }
}
