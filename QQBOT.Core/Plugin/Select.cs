using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using QQBOT.Core.MiraiHttp;
using QQBOT.Core.MiraiHttp.Entity;
using QQBOT.Core.Plugin.PluginEntity;
using QQBOT.Core.Util;

namespace QQBOT.Core.Plugin
{
    public class Select : PluginBase
    {
        private (string a, string b)? Parser(string msg)
        {
            msg = msg.Trim().TrimEnd('?').TrimEnd('？').TrimEnd('呢');

            var verb =
                "走 笑 有 在 看 写 飞 落 保护 开始 看　望　瞥　视　盯　瞧　窥　瞄　眺　瞪　瞅 俯视　遥望　凝视　探望　看护　鄙视　蔑视　斜视　歧视　打　环顾　 咬　吞　吐　吮　吸　啃　喝　吃　咀　嚼 搀　抱　搂　扶　捉　擒　掐　推　拿　抽　撕　摘　拣　捡　打　播　击　捏 撒　按　弹　撞　提　扭　捶　持　揍　披　捣　搜　托　举　拖　擦　敲　挖 抛　掘　抬　插　扔　写　抄　抓　捧　掷　撑　摊　倒　摔　劈　画　搔　撬 挥　揽　挡　捺　抚　搡　拉　摸　拍　摇　剪　拎　拔　拧　拨　舞　握　攥 退　进　奔　跑　赶　趋　遁　逃　立　站　跨　踢　跳　走　蹬　窜 说 看 走 听 笑 拿 跑 吃 唱 喝 敲 坐 盯 踢 闻 摸 批评 宣传 保卫 学习 研究 进行 开始 停止 禁止 在 死 有 等于 发生 演变 发展 生长 死亡 存在 消灭 想 爱 恨 伯 想念 打算 喜欢 希望 害伯 担心 讨厌 觉的 思考 是 为 乃 能 能够 会 可以 愿 肯 敢 要 应当 应该 配 值得 上 下 进 出 回 开 过 起 来 上来 下来 进来 出来 回来 开来 过来 起来 去 上去 下去 进去 出主 回去，开去 过去 生长 枯萎 发芽 结果 产卵 睡 日 草 操"
                    .Split(' ')
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Distinct().ToList();

            var prefix = msg.CheckPrefix(verb).ToList();

            if (prefix.Count == 0) return null;

            var (p, _) = prefix.First();

            var regex = new Regex($"{p}(.*?)还是(.*)");

            var match = regex.Match(msg);

            if (match.Groups.Count != 3) return null;

            var a = p + match.Groups[1].Value;
            var b = match.Groups[2].Value;

            if (b[0] == '不') return (a, b);
            return b.TrimStart(verb) == null ? (a, p + b) : (a, b);
        }

        protected override async Task<PluginTaskState> FriendMessageHandler(MiraiHttpSession session, Message message)
        {
            var msg = message.MessageChain!.PlainText;

            var res = Parser(msg);

            if (res == null) return PluginTaskState.NoResponse;

            var send = new Random().Next(0, 2) == 0 ? res.Value.a : res.Value.b;

            await session.SendFriendMessage(new Message(MessageChain.FromPlainText("建议" + send)), message.Sender!.Id);
            return PluginTaskState.CompletedTask;
        }

        protected override async Task<PluginTaskState> GroupMessageHandler(MiraiHttpSession session, Message message)
        {
            var msg = message.MessageChain!.PlainText;

            var res = Parser(msg);

            if (res == null) return PluginTaskState.NoResponse;

            var send = new Random().Next(0, 2) == 0 ? res.Value.a : res.Value.b;

            await session.SendGroupMessage(new Message(MessageChain.FromPlainText("建议" + send)),
                message.GroupInfo!.Id, message.Source.Id);

            return PluginTaskState.CompletedTask;
        }
    }
}