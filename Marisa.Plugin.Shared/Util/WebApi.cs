using PuppeteerSharp;

namespace Marisa.Plugin.Shared.Util;

public static class WebApi
{
    private const string Frontend = "http://localhost:14311";
    // private const string Frontend = "http://localhost:5173";
    private static IBrowser? _browserInner;
    private static readonly object BrowserLock = new();

    private static IBrowser Browser
    {
        get
        {
            lock (BrowserLock)
            {
                if (_browserInner is { IsClosed: false })
                {
                    return _browserInner;
                }

                return _browserInner = CreateBrowser().GetAwaiter().GetResult();
            }
        }
    }

    private static ScreenshotOptions ScreenshotOptions => new()
    {
        FullPage = true,
        Type     = ScreenshotType.Jpeg,
        Quality  = 90
    };

    private static async Task<IBrowser> CreateBrowser()
    {
        var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync();

        return await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            Args     = ["--force-device-scale-factor=1"]
        });
    }

    private static async Task<string> RenderUrl(string url)
    {
        await using var page = await Browser.NewPageAsync();

        await page.SetViewportAsync(new ViewPortOptions
        {
            Width = 1, Height = 1
        });

        await page.GoToAsync(url, new NavigationOptions
        {
            WaitUntil = [WaitUntilNavigation.Networkidle0, WaitUntilNavigation.Networkidle2, WaitUntilNavigation.Load, WaitUntilNavigation.DOMContentLoaded],
            // Disable Timeout
            Timeout = 0
        });

        return await page.ScreenshotBase64Async(ScreenshotOptions);
    }

    public static async Task<string> MaiMaiBest(Guid guid)
    {
        return await RenderUrl(Frontend + "/maimai/best?id=" + guid);
    }

    public static async Task<string> MaiMaiRank(Guid guid)
    {
        return await RenderUrl(Frontend + "/maimai/rank?id=" + guid);
    }

    public static async Task<string> OsuScore(string name, int modeInt, int? bpRank, bool recent, bool fail)
    {
        return await RenderUrl(Frontend + "/osu/score?" + "name=" + name + "&mode=" + modeInt + "&bpRank=" + (bpRank ?? 1) +
                               (recent ? "&recent=" + recent : "") +
                               (fail ? "&fail=" + fail : ""));
    }

    public static async Task<string> OsuRecommend(Guid contextId)
    {
        return await RenderUrl(Frontend + "/osu/recommend?id=" + contextId);
    }

    public static async Task<string> OsuPreview(long beatmapId)
    {
        return await RenderUrl(Frontend + "/osu/preview?id=" + beatmapId);
    }

    public static async Task<string> MaiMaiRecommend(Guid contextId)
    {
        return await RenderUrl(Frontend + "/maimai/recommend?id=" + contextId);
    }

    public static async Task<string> OngekiSong(int id)
    {
        return await RenderUrl(Frontend + $"/ongeki/song/{id}");
    }

    public static async Task<string> ChunithmSummary(Guid contextId)
    {
        return await RenderUrl(Frontend + "/chunithm/summary?id=" + contextId);
    }

    public static async Task<string> ChunithmOverPowerAll(Guid contextId)
    {
        return await RenderUrl(Frontend + "/chunithm/overpower?id=" + contextId);
    }

    public static async Task<string> ChunithmPreview(Guid contextId)
    {
        return await RenderUrl(Frontend + "/chunithm/preview?id=" + contextId);
    }

    public static async Task<string> ChunithmBest(Guid contextId)
    {
        return await RenderUrl(Frontend + "/chunithm/best?id=" + contextId);
    }
}