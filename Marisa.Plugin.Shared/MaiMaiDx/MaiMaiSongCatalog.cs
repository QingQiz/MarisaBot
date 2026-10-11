using System.Dynamic;
using Newtonsoft.Json;
using NLog;

namespace Marisa.Plugin.Shared.MaiMaiDx;

/// <summary>公共曲库按需刷新；服务异常时沿用上次成功资料，首次水鱼不可用时读静态曲库。</summary>
public sealed class MaiMaiSongCatalog(
    string fallbackFilePath,
    HttpClient? httpClient = null,
    TimeProvider? timeProvider = null)
{
    public const string DivingFishEndpoint = "https://www.diving-fish.com/api/maimaidxprober/music_data";
    public const string LxnsEndpoint = "https://maimai.lxns.net/api/v0/maimai/song/list?notes=true";
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(30);

    private static readonly HttpClient SharedHttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly HttpClient _httpClient = httpClient ?? SharedHttpClient;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private List<MaiMaiSong>? _divingFishSongs;
    private List<MaiMaiSong>? _lxnsSongs;
    private List<MaiMaiSong>? _songs;
    private DateTimeOffset _expiresAt;

    public void Invalidate() => _expiresAt = DateTimeOffset.MinValue;

    public async Task<List<MaiMaiSong>> GetAsync()
    {
        if (_songs != null && _timeProvider.GetUtcNow() < _expiresAt) return _songs;

        await _refreshLock.WaitAsync();
        try
        {
            if (_songs != null && _timeProvider.GetUtcNow() < _expiresAt) return _songs;

            var divingFish = FetchAsync(DivingFishEndpoint, ParseDivingFish);
            var lxns = FetchAsync(LxnsEndpoint, LxnsSongCatalog.FromJson);
            await Task.WhenAll(divingFish, lxns);

            var changed = _songs == null || divingFish.Result != null || lxns.Result != null;
            _divingFishSongs = divingFish.Result ?? _divingFishSongs;
            _lxnsSongs = lxns.Result ?? _lxnsSongs;
            if (changed)
            {
                var primary = _divingFishSongs ?? ParseDivingFish(await File.ReadAllTextAsync(fallbackFilePath));
                // 同 ID 优先保持水鱼原始资料；仅补入落雪先上线的真实谱面。
                _songs = primary.Concat(_lxnsSongs ?? []).DistinctBy(song => song.Id).ToList();
            }
            _expiresAt = _timeProvider.GetUtcNow() + RefreshInterval;
            return _songs!;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public static List<MaiMaiSong> ParseDivingFish(string json)
    {
        var data = JsonConvert.DeserializeObject<ExpandoObject[]>(json)
                   ?? throw new InvalidDataException("DivingFish song list is null");
        var songs = data.Select(song => new MaiMaiSong(song)).ToList();
        if (songs.Count == 0) throw new InvalidDataException("DivingFish song list is empty");
        return songs;
    }

    private async Task<List<MaiMaiSong>?> FetchAsync(string endpoint, Func<string, List<MaiMaiSong>> parse)
    {
        try
        {
            var songs = parse(await _httpClient.GetStringAsync(endpoint));
            if (songs.Count == 0) throw new InvalidDataException("Public song list is empty");
            return songs;
        }
        catch (Exception exception)
        {
            Logger.Warn(exception, "Unable to refresh public maimai catalog from {0}", endpoint);
            return null;
        }
    }
}
