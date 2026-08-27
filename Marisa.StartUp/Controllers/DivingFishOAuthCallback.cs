using Marisa.Plugin.Shared.DivingFish;
using Microsoft.AspNetCore.Mvc;

namespace Marisa.StartUp.Controllers;

/// <summary>
///     水鱼 OAuth 授权码回调页。
///     浏览器完成授权后跳到本页，bot 在此验证 state、换码、取 sub，并生成一次性绑定证明 C 显示给用户。
///     用户须在原 QQ 原群提交 C 完成绑定。
/// </summary>
[ApiController]
public class DivingFishOAuthCallback : Controller
{
    [HttpGet("/oauth/callback")]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
    {
        if (string.IsNullOrEmpty(error) == false)
        {
            return Content(SimpleHtml("授权未完成", $"水鱼返回错误：{error}"), "text/html; charset=utf-8");
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return Content(SimpleHtml("无效回调", "缺少 code 或 state 参数"), "text/html; charset=utf-8");
        }

        // 取待确认状态（含 PKCE verifier / QQ / 群）。state 不匹配或过期则拒绝。
        var pending = DivingFishPendingAuth.Take(state);
        if (pending == null)
        {
            return Content(SimpleHtml("回调已过期", "该授权请求已过期或已被使用，请重新发起绑定"), "text/html; charset=utf-8");
        }

        string sub, username, refreshToken;
        try
        {
            var (token, s, u) = await DivingFishOAuth.ExchangeAuthCode(code, pending.CodeVerifier);
            sub = s;
            username = u;
            refreshToken = token.RefreshToken;
        }
        catch (Exception e)
        {
            return Content(SimpleHtml("换码失败", e.Message), "text/html; charset=utf-8");
        }

        // 生成一次性证明 C（至少 128-bit）
        var proofCode = DivingFishBindingProof.Issue(
            pending.Qq.ToString(), pending.GroupId.ToString(), sub, username, refreshToken, pending.Game,
            DivingFishOAuth.ScopeOf(pending.Game), pending.Generation);

        var html = $@"<!DOCTYPE html>
<html lang=""zh-CN""><head><meta charset=""utf-8""><title>水鱼绑定确认</title></head>
<body style=""font-family:sans-serif;max-width:640px;margin:40px auto;line-height:1.8"">
<h2>水鱼账号绑定确认</h2>
<p>授权成功！你的水鱼账号：<b>{EscapeHtml(username)}</b></p>
<p>绑定目标：QQ <b>{pending.Qq}</b>（群 {pending.GroupId}）</p>
<p>以下是一次性确认码，<b>请复制后在原 QQ 的原群发送给机器人</b>：</p>
<p style=""font-size:24px;letter-spacing:2px;background:#f0f0f0;padding:12px;border-radius:6px""><code>{proofCode}</code></p>
<p style=""color:#c00"">⚠ 不要将确认码发给任何人！5 分钟内有效，仅能使用一次。</p>
</body></html>";

        return Content(html, "text/html; charset=utf-8");
    }

    private static string SimpleHtml(string title, string body) =>
        $"<!DOCTYPE html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\"><title>{EscapeHtml(title)}</title></head>" +
        $"<body style=\"font-family:sans-serif;max-width:640px;margin:40px auto\"><h2>{EscapeHtml(title)}</h2><p>{EscapeHtml(body)}</p></body></html>";

    private static string EscapeHtml(string? s) =>
        System.Net.WebUtility.HtmlEncode(s ?? "");
}
