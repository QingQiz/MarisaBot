using System.Text.Json;

namespace Marisa.Plugin.Shared.Util;

/// <summary>
///     把查分器（水鱼 / 落雪）的 HTTP 错误归类成给用户的可读提示，区分三类：
///     开发者令牌问题（机器人侧）、未绑定 / 查无此人、未公开成绩。
///     无法归类时回落到 "[查分器] 状态码: 原文"。
/// </summary>
public static class ProberError
{
    public const string DevTokenHint = "查分器令牌失效，请联系机器人管理员";
    public const string PrivacyHint  = "对方未公开成绩";

    public static string NotBound(string prober) => $"未绑定{prober}，或查不到该玩家";

    // 水鱼：开发者令牌错误放在 msg 字段，玩家身份错误放在 message 字段。
    private static readonly HashSet<string> DivingFishDevTokenMsgs =
        ["请先联系水鱼申请开发者token", "开发者token有误", "开发者token被禁用"];

    private static readonly HashSet<string> DivingFishPlayerMsgs =
        ["导入token有误", "尚未登录", "会话过期"];

    public static string DivingFish(int statusCode, string body, string prober = "水鱼")
    {
        var msg     = ReadStringField(body, "msg");
        var message = ReadStringField(body, "message");

        if (msg is not null && DivingFishDevTokenMsgs.Contains(msg)) return DevTokenHint;
        if (message is not null && DivingFishPlayerMsgs.Contains(message)) return NotBound(prober);

        return statusCode switch
        {
            400 or 401 => NotBound(prober),
            403        => PrivacyHint,
            _          => Fallback(prober, statusCode, message ?? msg),
        };
    }

    public static string Lxns(int statusCode, string body, string prober = "落雪") => statusCode switch
    {
        401        => DevTokenHint,
        400 or 404 => NotBound(prober),
        403        => PrivacyHint,
        _          => Fallback(prober, statusCode, ReadStringField(body, "message")),
    };

    private static string Fallback(string prober, int statusCode, string? detail) =>
        $"[{prober}] {statusCode}: {detail ?? "未知错误"}";

    private static string? ReadStringField(string body, string field)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty(field, out var e) && e.ValueKind == JsonValueKind.String
                ? e.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }
}
