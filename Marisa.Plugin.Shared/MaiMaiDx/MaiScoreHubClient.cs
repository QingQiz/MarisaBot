using System.Text.Json;
using Flurl.Http;

namespace Marisa.Plugin.Shared.MaiMaiDx;

/// <summary>
///     bakapiano 的 maimai-score-hub (MSH) 公开 API 的轻量客户端。
///     用于 <c>maimai 导</c> 命令把玩家的华立成绩推到他【本人】的水鱼 / 落雪查分器。
///     MSH 官方开放接入（公开 OpenAPI + 全开 CORS）；我们带一个专门的 User-Agent 表明身份。
///     契约见 https://github.com/bakapiano/maimai-score-hub/blob/main/shared/openapi/openapi.yaml
///     2026-07 起 MSH 迁移到 /api/v1 并拆分任务模型：登录任务只负责建立好友关系并下发 JWT，
///     抓分是独立的 update_score 任务（POST /me/dxnet-jobs），向查分器导出为异步任务需轮询结果。
/// </summary>
public class MaiScoreHubClient
{
    public const string BaseUrl = "https://maimai.bakapiano.com/api/v1";

    private const string UserAgent = "MarisaBot-maimai-sync/1.0 (+https://github.com/QingQiz/MarisaBot)";

    private static IFlurlRequest Req(string path) => $"{BaseUrl}{path}"
        .WithHeader("User-Agent", UserAgent)
        .WithTimeout(30);

    private static IFlurlRequest Authed(string path, string jwt) => Req(path)
        .WithHeader("Authorization", "Bearer " + jwt);

    public sealed record LoginRequestResult(string JobId, string? BotFriendCode, string? AuthToken, string? Message);

    public sealed record LoginStatusResult(bool Done, string Status, string? Stage, string? Token, string? Message, string? BotFriendCode);

    public sealed record ProfileResult(bool HasLxns, bool HasDivingFish);

    public sealed record ExportResult(bool Success, int Exported, int Scores, string? Message);

    public sealed record JobResult(string Status, string? Stage, string? Error);

    private static string? S(JsonElement e, string k) =>
        e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>POST /auth/login-requests — 用好友码发起登录任务（Bot 向用户发好友申请）。</summary>
    public async Task<LoginRequestResult> LoginRequestAsync(string friendCode)
    {
        var json = await Req("/auth/login-requests")
            .PostJsonAsync(new { friendCode, method = "bot_sends_request" })
            .ReceiveString();

        using var doc = JsonDocument.Parse(json);
        var root  = doc.RootElement;
        var jobId = S(root, "jobId") ?? "";

        // Bot 好友码可能位于根级（botFriendCode）或嵌套任务对象（botUserFriendCode，任务被
        // 调度前为空），两处都取
        var bot = S(root, "botFriendCode");
        if (bot == null && root.TryGetProperty("job", out var job) && job.ValueKind == JsonValueKind.Object)
        {
            bot = S(job, "botUserFriendCode");
        }

        var authToken = S(root, "authToken") ?? S(root, "token");
        return new LoginRequestResult(jobId, bot, authToken, S(root, "message"));
    }

    /// <summary>
    ///     GET /auth/login-requests/{jobId} — 轮询登录任务。好友关系确认（status 变为
    ///     completed）时响应为根级 {status, token, user}；进行中为 {status, job}，
    ///     stage / 失败原因 / Bot 好友码位于嵌套的 job 对象。
    /// </summary>
    public async Task<LoginStatusResult> LoginStatusAsync(string jobId)
    {
        var json = await Req($"/auth/login-requests/{jobId}").GetStringAsync();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var status = S(root, "status") ?? "";
        var token  = S(root, "token");

        string? stage = null, error = null, botFriendCode = null;
        if (root.TryGetProperty("job", out var job) && job.ValueKind == JsonValueKind.Object)
        {
            stage         = S(job, "stage");
            error         = S(job, "error");
            botFriendCode = S(job, "botUserFriendCode");
        }

        var done = status == "completed" || !string.IsNullOrEmpty(token);

        return new LoginStatusResult(done, status, stage, token, error ?? S(root, "message"), botFriendCode);
    }

    /// <summary>
    ///     POST /me/dxnet-jobs（Bearer）— 创建抓分任务。登录任务不再包含抓分，须在好友关系
    ///     确认后单独创建；friendshipJobId 传刚完成的登录任务号作为好友关系凭证，服务端可
    ///     立即开始抓分而无需等待 Bot 好友列表快照刷新。
    /// </summary>
    public async Task<string> CreateUpdateScoreJobAsync(string jwt, string friendshipJobId)
    {
        var resp = await Authed("/me/dxnet-jobs", jwt)
            .AllowHttpStatus("400-499")
            .PostJsonAsync(new { jobType = "update_score", friendshipJobId });

        var json = await resp.GetStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (resp.StatusCode >= 400)
        {
            throw new InvalidOperationException(S(root, "message") ?? S(root, "error") ?? $"HTTP {resp.StatusCode}");
        }

        var jobId = S(root, "jobId");
        if (string.IsNullOrEmpty(jobId)) throw new InvalidOperationException("创建抓分任务失败（响应中无任务号）");
        return jobId!;
    }

    /// <summary>GET /me/dxnet-jobs/{jobId}（Bearer）— 查询抓分任务状态。</summary>
    public async Task<JobResult> GetJobAsync(string jwt, string jobId)
    {
        var json = await Authed($"/me/dxnet-jobs/{jobId}", jwt).GetStringAsync();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return new JobResult(S(root, "status") ?? "", S(root, "stage"), S(root, "error"));
    }

    /// <summary>GET /me（Bearer）— 看 MSH 里已配置了哪些查分器令牌。</summary>
    public async Task<ProfileResult> GetProfileAsync(string jwt)
    {
        var json = await Authed("/me", jwt).GetStringAsync();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        bool B(string k) => root.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.True;

        return new ProfileResult(B("hasLxnsImportToken"), B("hasDivingFishImportToken"));
    }

    /// <summary>
    ///     PATCH /me（Bearer）— 设置某查分器的导入令牌。抓分完成后向已配置令牌的查分器
    ///     推送由本客户端显式触发；不改动 autoUpdate（那是 MSH 的定时自动更新开关，
    ///     是否开启应由用户在网站上自行决定）。
    /// </summary>
    public async Task SetTokenAsync(string jwt, string prober, string token)
    {
        object body = prober == "lxns"
            ? new { lxnsImportToken = token }
            : new { divingFishImportToken = token };

        await Authed("/me", jwt).PatchJsonAsync(body);
    }

    /// <summary>
    ///     POST /me/sync/latest/exports/{prober}（Bearer）— 把抓到的成绩推到用户自己的查分器。
    ///     导出已改为异步任务：创建后轮询 GET /me/sync/prober-export-jobs/{id} 直至终态，
    ///     单查分器的回执位于任务 result 的对应字段（divingFish / lxns）。
    /// </summary>
    public async Task<ExportResult> ExportAsync(string jwt, string prober)
    {
        var path = prober == "lxns" ? "/me/sync/latest/exports/lxns" : "/me/sync/latest/exports/diving-fish";

        var resp = await Authed(path, jwt)
            .AllowHttpStatus("400-499")
            .PostJsonAsync(new { });

        var json = await resp.GetStringAsync();

        if (resp.StatusCode >= 400)
        {
            using var err = JsonDocument.Parse(json);
            return new ExportResult(false, 0, 0, S(err.RootElement, "message") ?? S(err.RootElement, "error") ?? $"HTTP {resp.StatusCode}");
        }

        string exportJobId;
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (TryParseExportJob(root, prober, out var immediate)) return immediate;
            exportJobId = S(root, "exportJobId") ?? "";
        }

        if (exportJobId.Length == 0) return new ExportResult(false, 0, 0, "创建导出任务失败（响应中无任务号）");

        // 队列繁忙时导出任务需要排队，容忍偶发的查询失败，最长等待 5 分钟
        var deadline = DateTime.UtcNow.AddMinutes(5);
        var failures = 0;
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(3000);

            string body;
            try
            {
                body     = await Authed($"/me/sync/prober-export-jobs/{exportJobId}", jwt).GetStringAsync();
                failures = 0;
            }
            catch
            {
                if (++failures >= 6) return new ExportResult(false, 0, 0, "查询导出任务状态失败");
                continue;
            }

            using var doc = JsonDocument.Parse(body);
            if (TryParseExportJob(doc.RootElement, prober, out var result)) return result;
        }

        return new ExportResult(false, 0, 0, "导出任务等待超时");
    }

    /// <summary>任务达到终态时解析对应查分器的回执；job 既可能是响应根级也可能嵌套于 job 字段。</summary>
    private static bool TryParseExportJob(JsonElement root, string prober, out ExportResult result)
    {
        var job = root.TryGetProperty("job", out var j) && j.ValueKind == JsonValueKind.Object ? j : root;

        var status = S(job, "status") ?? S(root, "status") ?? "";
        if (status is not ("completed" or "partial_failed" or "failed" or "skipped"))
        {
            result = default!;
            return false;
        }

        var key = prober == "lxns" ? "lxns" : "divingFish";
        if (job.TryGetProperty("result", out var r) && r.ValueKind == JsonValueKind.Object &&
            r.TryGetProperty(key, out var pr) && pr.ValueKind == JsonValueKind.Object)
        {
            int I(string k) => pr.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : 0;

            var ok = S(pr, "status") == "success";
            result = new ExportResult(ok, I("exported"), I("scores"), S(pr, "message"));
            return true;
        }

        result = new ExportResult(status == "completed", 0, 0, S(job, "error") ?? status);
        return true;
    }
}
