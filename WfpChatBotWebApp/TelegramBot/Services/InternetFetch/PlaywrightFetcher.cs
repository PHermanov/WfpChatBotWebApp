using Microsoft.Playwright;

namespace WfpChatBotWebApp.TelegramBot.Services.InternetFetch;

public class PlaywrightFetcher : IPageFetcher, IAsyncDisposable
{
    private readonly SemaphoreSlim _browserLock = new(1, 1);
    private IBrowser _browser;
    private IPlaywright _playwright;

    private readonly int _timeoutMs = 15000;
    private readonly int _maxRetries = 2;

    public async Task<string> Fetch(string url, CancellationToken ct = default)
    {
        for (int attempt = 0; attempt <= _maxRetries; attempt++)
        {
            try
            {
                await EnsureBrowser();

                var context = await _browser.NewContextAsync(new BrowserNewContextOptions
                {
                    UserAgent = GetUserAgent(),
                    ViewportSize = new() { Width = 1280, Height = 800 },
                    Locale = "en-US"
                });

                var page = await context.NewPageAsync();

                await page.GotoAsync(url, new PageGotoOptions
                {
                    Timeout = _timeoutMs,
                    WaitUntil = WaitUntilState.DOMContentLoaded
                });

                await HandleCookieBanners(page);

                await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new()
                {
                    Timeout = _timeoutMs
                });

                var html = await page.ContentAsync();

                await context.CloseAsync();

                return html;
            }
            catch
            {
                if (attempt == _maxRetries)
                    throw;

                await Task.Delay(500 * (attempt + 1), ct);
            }
        }

        return "";
    }

    private async Task EnsureBrowser()
    {
        if (_browser != null)
            return;

        await _browserLock.WaitAsync();

        try
        {
            if (_browser == null)
            {
                _playwright = await Playwright.CreateAsync();

                _browser = await _playwright.Chromium.LaunchAsync(
                    new BrowserTypeLaunchOptions
                    {
                        Headless = true,
                        Args = new[]
                        {
                            "--disable-blink-features=AutomationControlled",
                            "--no-sandbox",
                            "--disable-dev-shm-usage"
                        }
                    });
            }
        }
        finally
        {
            _browserLock.Release();
        }
    }

    // 🔥 Cookie banner heuristic
    private async Task HandleCookieBanners(IPage page)
    {
        var selectors = new[]
        {
            "button:has-text('accept')",
            "button:has-text('agree')",
            "button:has-text('got it')",
            "button:has-text('allow')",
            "button:has-text('yes')",
            "text=Accept all",
            "text=I agree",
            "[aria-label*='accept']",
            "[id*='accept']"
        };

        foreach (var sel in selectors)
        {
            try
            {
                var element = await page.QuerySelectorAsync(sel);

                if (element != null)
                {
                    await element.ClickAsync(new() { Timeout = 1000 });
                    return;
                }
            }
            catch { }
        }

        // fallback
        var buttons = await page.QuerySelectorAllAsync("button");

        foreach (var btn in buttons)
        {
            try
            {
                var text = (await btn.InnerTextAsync()).ToLower();

                if (IsAcceptText(text))
                {
                    await btn.ClickAsync();
                    return;
                }
            }
            catch { }
        }
    }

    private bool IsAcceptText(string text)
    {
        return text.Contains("accept")
            || text.Contains("agree")
            || text.Contains("allow")
            || text.Contains("ok")
            || text.Contains("got it")
            || text.Contains("consent");
    }

    private static string GetUserAgent()
    {
        return "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
               "(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser != null)
            await _browser.CloseAsync();

        _playwright?.Dispose();
    }
}