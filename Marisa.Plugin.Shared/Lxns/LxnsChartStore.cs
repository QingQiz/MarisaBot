using Flurl.Http;

namespace Marisa.Plugin.Shared.Lxns;

/// <summary>
/// lxns 谱面文本（simai 格式）的本地缓存。谱面来自公开 CDN，按歌曲 id 落盘，
/// 每首歌最多访问一次远端，避免对 lxns 产生重复请求
/// </summary>
public static class LxnsChartStore
{
    private const string ChartCdnUrl = "https://assets2.lxns.net/maimai/chart";

    private static readonly SemaphoreSlim FetchLock = new(1, 1);

    private static string CachePath
    {
        get
        {
            var path = Path.Join(MaiMaiDx.ResourceManager.TempPath, "LxnsChart");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    /// <summary>
    /// 获取歌曲的 simai 谱面文本，本地无缓存时从 CDN 拉取。谱面不存在时返回 null
    /// </summary>
    public static async Task<string?> GetChart(long songId)
    {
        var cache = Path.Join(CachePath, $"{songId}.txt");

        if (File.Exists(cache))
        {
            return await File.ReadAllTextAsync(cache);
        }

        await FetchLock.WaitAsync();
        try
        {
            if (File.Exists(cache))
            {
                return await File.ReadAllTextAsync(cache);
            }

            var response = await $"{ChartCdnUrl}/{songId}.txt".AllowHttpStatus("404").GetAsync();
            if (response.StatusCode == 404)
            {
                return null;
            }

            var chart = await response.GetStringAsync();
            if (string.IsNullOrWhiteSpace(chart))
            {
                return null;
            }

            await File.WriteAllTextAsync(cache, chart);
            return chart;
        }
        finally
        {
            FetchLock.Release();
        }
    }
}
