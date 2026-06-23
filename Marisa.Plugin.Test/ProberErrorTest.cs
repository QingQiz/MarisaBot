using Marisa.Plugin.Shared.Util;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class ProberErrorTest
{
    [Test]
    public void Lxns_ClassifiesByStatus()
    {
        Assert.That(ProberError.Lxns(401, "{}"), Is.EqualTo(ProberError.DevTokenHint));
        Assert.That(ProberError.Lxns(400, "{}"), Is.EqualTo(ProberError.NotBound("落雪")));
        Assert.That(ProberError.Lxns(404, "{}"), Is.EqualTo(ProberError.NotBound("落雪")));
        Assert.That(ProberError.Lxns(403, "{}"), Is.EqualTo(ProberError.PrivacyHint));
    }

    [Test]
    public void Lxns_FallsBackOnUnknownStatus()
    {
        Assert.That(ProberError.Lxns(500, "{\"message\":\"server error\"}"), Is.EqualTo("[落雪] 500: server error"));
        Assert.That(ProberError.Lxns(500, "not json"), Is.EqualTo("[落雪] 500: 未知错误"));
    }

    // 水鱼：开发者令牌错误在 msg 字段（机器人侧问题）。
    [Test]
    public void DivingFish_DevTokenViaMsgField()
    {
        Assert.That(ProberError.DivingFish(403, "{\"msg\":\"开发者token有误\"}"), Is.EqualTo(ProberError.DevTokenHint));
        Assert.That(ProberError.DivingFish(400, "{\"msg\":\"请先联系水鱼申请开发者token\"}"), Is.EqualTo(ProberError.DevTokenHint));
    }

    // 水鱼：玩家身份错误在 message 字段，优先级高于状态码。
    [Test]
    public void DivingFish_PlayerViaMessageField()
    {
        Assert.That(ProberError.DivingFish(400, "{\"message\":\"尚未登录\"}"), Is.EqualTo(ProberError.NotBound("水鱼")));
        Assert.That(ProberError.DivingFish(403, "{\"message\":\"会话过期\"}"), Is.EqualTo(ProberError.NotBound("水鱼")));
    }

    [Test]
    public void DivingFish_FallsBackToStatusWhenNoRecognizedField()
    {
        Assert.That(ProberError.DivingFish(400, "{}"), Is.EqualTo(ProberError.NotBound("水鱼")));
        Assert.That(ProberError.DivingFish(403, "{}"), Is.EqualTo(ProberError.PrivacyHint));
        Assert.That(ProberError.DivingFish(403, "not json"), Is.EqualTo(ProberError.PrivacyHint));
    }
}
