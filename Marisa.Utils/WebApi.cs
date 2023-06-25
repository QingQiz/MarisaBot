using PuppeteerSharp;

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
    
    private static WaitForNetworkIdleOptions NetworkIdleOptions => new()
    {
        Timeout = 0,
        IdleTime = 0
    };

    public static async Task<string> MaiMaiBest(string? username, long? qq, bool b50)
    {
        await using var page = Page;

        if (!string.IsNullOrWhiteSpace(username))
        {
            await page.GoToAsync(Frontend + "/maimai/best?" + "username=" + username + (b50 ? "&b50=" + b50 : ""));
        }
        else
        {
            await page.GoToAsync(Frontend + "/maimai/best?" + "qq=" + qq + (b50 ? "&b50=" + b50 : ""));
        }

        await page.WaitForNetworkIdleAsync(NetworkIdleOptions);
        return await page.ScreenshotBase64Async(new ScreenshotOptions { FullPage = true });
    }

    public static async Task<string> OsuScore(string name, int modeInt, int bpRank, bool recent, bool fail)
    {
        await using var page = Page;

        await page.GoToAsync(Frontend + "/osu/score?" + "name=" + name + "&mode=" + modeInt + "&bpRank=" + bpRank +
            (recent ? "&recent=" + recent : "") +
            (fail ? "&fail=" + fail : ""));
        await page.WaitForNetworkIdleAsync(NetworkIdleOptions);
        return await page.ScreenshotBase64Async(new ScreenshotOptions { FullPage   = true });
    }

    public static async Task<string> OsuRecommend(long uid, int modeInt)
    {
        await using var page = Page;

        await page.GoToAsync(Frontend + "/osu/recommend?" + "uid=" + uid + "&mode=" + modeInt);
        await page.WaitForNetworkIdleAsync(NetworkIdleOptions);
        return await page.ScreenshotBase64Async(new ScreenshotOptions { FullPage   = true });
    }
}