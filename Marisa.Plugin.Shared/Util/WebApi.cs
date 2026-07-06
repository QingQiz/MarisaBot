using System.Runtime.Loader;
using Marisa.Configuration;
using NLog;
using PuppeteerSharp;

namespace Marisa.Plugin.Shared.Util;

public static class WebApi
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static IBrowser? _browserInner;
    private static readonly object BrowserLock = new();

    private static string PrivateFrontend => ConfigurationManager.Configuration.Web.PrivateBaseUrl;

    private static string PublicFrontend => ConfigurationManager.Configuration.Web.PublicBaseUrl;

    static WebApi()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => CloseBrowserOnShutdown();
        AssemblyLoadContext.Default.Unloading += _ => CloseBrowserOnShutdown();
    }

    public static bool DisableWebApi { get; set; }

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

    public static async Task CloseBrowserAsync()
    {
        IBrowser? browser;
        lock (BrowserLock)
        {
            browser = _browserInner;
            _browserInner = null;
        }

        if (browser is null)
        {
            return;
        }

        try
        {
            if (!browser.IsClosed)
            {
                await browser.CloseAsync();
            }
        }
        finally
        {
            await browser.DisposeAsync();
        }
    }

    private static void CloseBrowserOnShutdown()
    {
        try
        {
            CloseBrowserAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // Shutdown should not be blocked by browser cleanup errors.
        }
    }

    public static async Task<string> RenderUrl(string url)
    {
        if (DisableWebApi)
        {
            return "";
        }

        var privateUrl = CombineUrl(PrivateFrontend, url);

        try
        {
            await using var page = await Browser.NewPageAsync();

            await page.SetViewportAsync(new ViewPortOptions
            {
                Width = 1, Height = 1
            });

            await page.GoToAsync(privateUrl, new NavigationOptions
            {
                WaitUntil = [WaitUntilNavigation.Networkidle0, WaitUntilNavigation.Networkidle2, WaitUntilNavigation.Load, WaitUntilNavigation.DOMContentLoaded],
                // Disable Timeout
                Timeout = 0
            });

            return await page.ScreenshotBase64Async(ScreenshotOptions);
        }
        catch (Exception e)
        {
            var publicUrl  = CombineUrl(PublicFrontend, url);
            Logger.Warn(e, "Failed to render screenshot for {0}; public URL fallback is {1}", privateUrl, publicUrl);
            throw new WebRenderFailedException(privateUrl, publicUrl, e);
        }
    }

    private static string CombineUrl(string baseUrl, string relativeUrl)
    {
        if (Uri.TryCreate(relativeUrl, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.ToString();
        }

        if (relativeUrl.StartsWith('/'))
        {
            return $"{baseUrl}{relativeUrl}";
        }

        return $"{baseUrl}/{relativeUrl}";
    }

    public static async Task<string> MaiMaiBest(Guid guid)
    {
        return await RenderUrl("/maimai/best?id=" + guid);
    }

    public static async Task<string> MaiMaiRank(Guid guid)
    {
        return await RenderUrl("/maimai/rank?id=" + guid);
    }

    public static async Task<string> OsuScore(string name, int modeInt, int? bpRank, bool recent, bool fail)
    {
        return await RenderUrl("/osu/score?" + "name=" + name + "&mode=" + modeInt + "&bpRank=" + (bpRank ?? 1) +
                               (recent ? "&recent=" + recent : "") +
                               (fail ? "&fail=" + fail : ""));
    }

    public static async Task<string> OsuRecommend(Guid contextId)
    {
        return await RenderUrl("/osu/recommend?id=" + contextId);
    }

    public static async Task<string> OsuPreview(long beatmapId)
    {
        return await RenderUrl("/osu/preview?id=" + beatmapId);
    }

    public static async Task<string> MaiMaiRecommend(Guid contextId)
    {
        return await RenderUrl("/maimai/recommend?id=" + contextId);
    }

    public static async Task<string> MaiMaiSummary(Guid contextId)
    {
        return await RenderUrl("/maimai/summary?id=" + contextId);
    }

    public static async Task<string> MaiMaiSong(Guid contextId)
    {
        return await RenderUrl("/maimai/song?id=" + contextId);
    }

    public static async Task<string> MaiMaiSongScore(Guid contextId)
    {
        return await RenderUrl("/maimai/song-score?id=" + contextId);
    }

    public static async Task<string> MaiMaiSongTitles(Guid contextId)
    {
        return await RenderUrl("/maimai/song-titles?id=" + contextId);
    }

    public static async Task<string> MaiMaiDifficultyCurve(long songId, int? levelIdx)
    {
        return await RenderUrl("/maimai/difficulty-curve?id=" + songId + (levelIdx == null ? "" : "&idx=" + levelIdx));
    }

    public static async Task<string> MaiMaiDifficultyCurveRank(string kind, string value)
    {
        return await RenderUrl($"/maimai/curve-rank?{kind}={Uri.EscapeDataString(value)}");
    }

    public static async Task<string> MaiMaiDanCourse(string version, string dani)
    {
        return await RenderUrl("/maimai/dan-course?ver=" + version + "&dani=" + Uri.EscapeDataString(dani));
    }

    public static async Task<string> OngekiSong(int id)
    {
        return await RenderUrl($"/ongeki/song/{id}");
    }

    public static async Task<string> ChunithmSong(Guid id)
    {
        return await RenderUrl("/chunithm/song?id=" + id);
    }

    public static async Task<string> ChunithmSummary(Guid contextId)
    {
        return await RenderUrl("/chunithm/summary?id=" + contextId);
    }

    public static async Task<string> ChunithmOverPowerAll(Guid contextId)
    {
        return await RenderUrl("/chunithm/overpower?id=" + contextId);
    }

    public static async Task<string> ChunithmOpBase(Guid contextId)
    {
        return await RenderUrl("/chunithm/op-base?id=" + contextId);
    }

    public static async Task<string> ChunithmOpGenre(Guid contextId)
    {
        return await RenderUrl("/chunithm/op-genre?id=" + contextId);
    }

    public static async Task<string> ChunithmOpLevel(Guid contextId)
    {
        return await RenderUrl("/chunithm/op-level?id=" + contextId);
    }

    public static async Task<string> ChunithmOpVersion(Guid contextId)
    {
        return await RenderUrl("/chunithm/op-version?id=" + contextId);
    }

    public static async Task<string> ChunithmPreview(Guid contextId)
    {
        return await RenderUrl("/chunithm/preview?id=" + contextId);
    }

    public static async Task<string> ChunithmBest(Guid contextId, bool b50)
    {
        if (b50)
        {
            return await RenderUrl($"/chunithm/best?id={contextId}&b50=1");
        }
        return await RenderUrl($"/chunithm/best?id={contextId}");
    }
}
