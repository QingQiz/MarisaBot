﻿using PuppeteerSharp;

namespace Marisa.Utils;

public static class WebApi
{
    private static IBrowser? _browserInner;

    private static readonly string Frontend = Environment.GetEnvironmentVariable("DEV") == null
        ? "http://localhost:14311"
        : "http://localhost:3000";

    private static IBrowser Browser
    {
        get
        {
            if (_browserInner is not null)
            {
                if (!_browserInner.IsClosed) return _browserInner;

                _browserInner = null;
                return Browser;
            }

            lock (Frontend)
            {
                using var browserFetcher = new BrowserFetcher();
                browserFetcher.DownloadAsync().Wait();
                _browserInner = Puppeteer.LaunchAsync(new LaunchOptions { Headless = true }).Result;
                return _browserInner;
            }
        }
    }

    private static IPage Page
    {
        get
        {
            var page = Browser.NewPageAsync().Result;
            page.BringToFrontAsync().Wait();
            page.SetViewportAsync(new ViewPortOptions
            {
                Width = 1, Height = 1
            });
            return page;
        }
    }
    
    private static async Task<string> RenderUrl(string url)
    {
        await using var page = Page;

        await page.GoToAsync(url);
        await page.WaitForNetworkIdleAsync(NetworkIdleOptions);
        return await page.ScreenshotBase64Async(ScreenshotOptions);
    }

    private static WaitForNetworkIdleOptions NetworkIdleOptions => new()
    {
        Timeout  = 0,
        IdleTime = 1000
    };

    private static ScreenshotOptions ScreenshotOptions => new()
    {
        FullPage = true,
        Type     = ScreenshotType.Jpeg,
        Quality  = 90
    };

    public static async Task<string> MaiMaiBest(Guid guid)
    {
        await using var page = Page;

        await page.GoToAsync(Frontend + "/maimai/best?id=" + guid);
        await page.WaitForNetworkIdleAsync(NetworkIdleOptions);

        return await page.ScreenshotBase64Async(ScreenshotOptions);
    }

    public static async Task<string> OsuScore(string name, int modeInt, int? bpRank, bool recent, bool fail)
    {
        await using var page = Page;

        await page.GoToAsync(Frontend + "/osu/score?" + "name=" + name + "&mode=" + modeInt + "&bpRank=" + (bpRank ?? 1) +
            (recent ? "&recent=" + recent : "") +
            (fail ? "&fail=" + fail : ""));
        await page.WaitForNetworkIdleAsync(NetworkIdleOptions);
        return await page.ScreenshotBase64Async(ScreenshotOptions);
    }

    public static async Task<string> OsuRecommend(Guid contextId)
    {
        await using var page = Page;

        await page.GoToAsync(Frontend + "/osu/recommend?id=" + contextId);
        await page.WaitForNetworkIdleAsync(NetworkIdleOptions);
        return await page.ScreenshotBase64Async(ScreenshotOptions);
    }

    public static async Task<string> OsuPreview(Guid contextId)
    {
        await using var page = Page;

        await page.GoToAsync(Frontend + "/osu/preview?id=" + contextId);
        await page.WaitForNetworkIdleAsync(NetworkIdleOptions);
        return await page.ScreenshotBase64Async(ScreenshotOptions);
    }

    public static async Task<string> MaiMaiRecommend(Guid contextId)
    {
        await using var page = Page;

        await page.GoToAsync(Frontend + "/maimai/recommend?id=" + contextId);
        await page.WaitForNetworkIdleAsync(NetworkIdleOptions);
        return await page.ScreenshotBase64Async(ScreenshotOptions);
    }

    public static async Task<string> OngekiSong(int id)
    {
        await using var page = Page;

        await page.GoToAsync(Frontend + $"/ongeki/song/{id}");
        await page.WaitForNetworkIdleAsync(NetworkIdleOptions);
        return await page.ScreenshotBase64Async(ScreenshotOptions);
    }
    
    public static async Task<string> ChunithmSummary(Guid contextId)
    {
        return await RenderUrl(Frontend + "/chunithm/summary?id=" + contextId);
    }

    public static async Task<string> ChunithmOverPowerAll(Guid contextId)
    {
        return await RenderUrl(Frontend + "/chunithm/overpower?id=" + contextId);
    }
}