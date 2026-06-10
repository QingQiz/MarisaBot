using System.Text.Json;
using Flurl.Http;

namespace Marisa.Plugin.Shared.MaiMaiDx;

/// <summary>
///     bakapiano 的 maimai-score-hub (MSH) 公开 API 的轻量客户端。
///     用于 <c>maimai 导</c> 命令把玩家的华立成绩推到他【本人】的水鱼 / 落雪查分器。
///     MSH 官方开放接入（公开 OpenAPI + 全开 CORS）；我们带一个专门的 User-Agent 表明身份。
///     契约见 https://github.com/bakapiano/maimai-score-hub/blob/main/shared/openapi/openapi.yaml
/// </summary>
public class MaiScoreHubClient
{
    public const string BaseUrl = "https://maimai.bakapiano.com/api";

    private const string UserAgent = "MarisaBot-maimai-sync/1.0 (+https://github.com/QingQiz/MarisaBot)";

    private static IFlurlRequest Req(string path) => $"{BaseUrl}{path}"
        .WithHeader("User-Agent", UserAgent)
        .WithTimeout(30);

    private static IFlurlRequest Authed(string path, string jwt) => Req(path)
        .WithHeader("Authorization", "Bearer " + jwt);

    public sealed record LoginRequestResult(string JobId, string? BotFriendCode, string? Message);

    public sealed record LoginStatusResult(bool Done, string Status, string? Stage, string? Token, string? Message);

    public sealed record ProfileResult(bool HasLxns, bool HasDivingFish, bool AutoLxns, bool AutoDivingFish);

    public sealed record ExportResult(bool Success, int Exported, int Scores, string? Message);

    /// <summary>POST /auth/login-request — 用好友码发起登录+抓分任务。</summary>
    public async Task<LoginRequestResult> LoginRequestAsync(string friendCode)
    {
        var json = await Req("/auth/login-request")
            .PostJsonAsync(new { friendCode, skipUpdateScore = false, useIdleUpdate = false })
            .ReceiveString();

        using var doc = JsonDocument.Parse(json);
        var root  = doc.RootElement;
        var jobId = root.TryGetProperty("jobId", out var j) ? j.GetString() ?? "" : "";

        string? bot = null;
        if (root.TryGetProperty("job", out var job) && job.ValueKind == JsonValueKind.Object &&
            job.TryGetProperty("botUserFriendCode", out var b) && b.ValueKind == JsonValueKind.String)
        {
            bot = b.GetString();
        }

        var msg = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
        return new LoginRequestResult(jobId, bot, msg);
    }

    /// <summary>GET /auth/login-status?jobId= — 轮询任务，完成时附带 JWT。</summary>
    public async Task<LoginStatusResult> LoginStatusAsync(string jobId)
    {
        var json = await Req("/auth/login-status").SetQueryParam("jobId", jobId).GetStringAsync();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        string? S(string k) => root.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        var status = S("status") ?? "";
        var token  = S("token");
        var done   = root.TryGetProperty("done", out var d) && d.ValueKind == JsonValueKind.True;

        // 拿到 token（或终态 completed）就算抓分完成
        if (!string.IsNullOrEmpty(token) || status == "completed") done = true;

        return new LoginStatusResult(done, status, S("stage"), token, S("message"));
    }

    /// <summary>GET /users/profile（Bearer）— 看 MSH 里已配置了哪些查分器令牌。</summary>
    public async Task<ProfileResult> GetProfileAsync(string jwt)
    {
        var json = await Authed("/users/profile", jwt).GetStringAsync();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        bool B(string k) => root.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;

        return new ProfileResult(B("hasLxnsImportToken"), B("hasDivingFishImportToken"), B("autoExportLxns"), B("autoExportDivingFish"));
    }

    /// <summary>PATCH /users/profile（Bearer）— 设置某查分器导入令牌并开启自动导出。</summary>
    public async Task SetTokenAsync(string jwt, string prober, string token)
    {
        object body = prober == "lxns"
            ? new { lxnsImportToken = token, autoExportLxns = true }
            : new { divingFishImportToken = token, autoExportDivingFish = true };

        await Authed("/users/profile", jwt).PatchJsonAsync(body);
    }

    /// <summary>POST /sync/latest/{prober}（Bearer）— 把抓到的成绩推到用户自己的查分器。</summary>
    public async Task<ExportResult> ExportAsync(string jwt, string prober)
    {
        var path = prober == "lxns" ? "/sync/latest/lxns" : "/sync/latest/diving-fish";

        var json = await Authed(path, jwt)
            .AllowHttpStatus("400-499")
            .PostJsonAsync(new { })
            .ReceiveString();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        int I(string k) => root.TryGetProperty(k, out var v) && v.TryGetInt32(out var n) ? n : 0;

        var success = root.TryGetProperty("success", out var s) && s.ValueKind == JsonValueKind.True;
        var msg     = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;

        return new ExportResult(success, I("exported"), I("scores"), msg);
    }
}
