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
                _browserInner = Puppeteer.LaunchAsync(new LaunchOptions { Headless = false }).Result;
                return _browserInner;
            }
        }
    }

    public static async Task<string> MaiMaiBest(string? username, long? qq, bool b50)
    {
        await using var page = await Browser.NewPageAsync();
        await page.BringToFrontAsync();

        if (!string.IsNullOrWhiteSpace(username))
        {
            await page.GoToAsync(Frontend + "/maimai/best?" + "username=" + username + (b50 ? "&b50=" + b50 : ""));
        }
        else
        {
            await page.GoToAsync(Frontend + "/maimai/best?" + "qq=" + qq + (b50 ? "&b50=" + b50 : ""));
        }

        await page.WaitForNetworkIdleAsync();
        return await page.ScreenshotBase64Async(new ScreenshotOptions { FullPage = true });
    }
}