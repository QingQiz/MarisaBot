using System.Net.WebSockets;
using System.Text.RegularExpressions;
using log4net;
using Marisa.EntityFrameworkCore;
using Marisa.Plugin.Shared.FSharp.Osu;
using Marisa.Plugin.Shared.Osu;
using Websocket.Client;
using Websocket.Client.Models;

namespace Marisa.Plugin.Osu;

public partial class Osu
{
    private readonly WebsocketClient _wsClient;
    private readonly ILog _logger;
    private readonly Dictionary<long, (Message Message, DateTime Time)> _waiting = new();

    public Osu(ILog logger)
    {
        _logger = logger;
        var factory = new Func<ClientWebSocket>(() =>
        {
            var client = new ClientWebSocket
            {
                Options =
                {
                    KeepAliveInterval = TimeSpan.FromSeconds(5),
                }
            };
            client.Options.SetRequestHeader("X-Self-ID", "2096937554");
            client.Options.SetRequestHeader("X-Client-Role", "Universal");
            return client;
        });

        _wsClient                  = new WebsocketClient(new Uri("ws://botws.desu.life:65000"), factory);
        _wsClient.ReconnectTimeout = TimeSpan.MaxValue;
    }

    public override async Task BackgroundService()
    {
        void OnNext(ResponseMessage next)
        {
            var t  = new Regex(@"""message"":""(.*?)""").Match(next.Text).Groups[1].Value;
            var id = long.Parse(new Regex(@"""user_id"":(\d*)").Match(next.Text).Groups[1].Value);

            Message message;

            lock (_waiting)
            {
                if (!_waiting.ContainsKey(id)) return;

                message = _waiting[id].Message;
            }

            if (string.IsNullOrEmpty(t))
            {
                message.Reply("猫猫不理魔理沙！");
            }
            else
            {
                if (t.StartsWith("[CQ:image"))
                {
                    var regex1 = new Regex(@"\[CQ:image,file=base64://(.*?)\]");
                    message.Reply(MessageDataImage.FromBase64(regex1.Match(t).Groups[1].Value));
                }
                else
                {
                    message.Reply(t.Replace("猫猫", "魔理沙问猫猫，她说她"));
                }
            }

            lock (_waiting) _waiting.Remove(id);
        }

        void OnReconnect(ReconnectionInfo info)
        {
            _logger.Warn("Reconnect to 猫猫");
        }

        _wsClient.MessageReceived.Subscribe(OnNext);
        _wsClient.ReconnectionHappened.Subscribe(OnReconnect);

        await _wsClient.Start();
    }

    private void AddCommandToQueue(Message message, string command)
    {
        var userId = message.Sender!.Id;
        var cmd    = BuildCommand(command, userId);

        _wsClient.Send(cmd);

        lock (_waiting)
        {
            if (_waiting.ContainsKey(userId))
            {
                var wait = _waiting[userId];

                if (DateTime.Now - wait.Time > TimeSpan.FromMinutes(1))
                {
                    _waiting[userId] = (message, DateTime.Now);
                }
                else
                {
                    message.Reply(new[] { "你先别急", "别急", "有点急", "急你妈" }.RandomTake());
                }
            }
            else
            {
                _waiting.Add(userId, (message, DateTime.Now));
            }
        }
    }

    /// <summary>
    /// 模拟 go-cqhttp 的消息
    /// </summary>
    /// <param name="command">猫猫 bot 需要的命令</param>
    /// <param name="userId">qq</param>
    /// <returns>go-cqhttp 生成的消息（json字符串）</returns>
    private static string BuildCommand(string command, long userId)
    {
        return
            @$"{{""font"":0,""message"":""!{command}"",""message_id"":0,""message_type"":""private"",""post_type"":""message"",""self_id"":0,""sender"":{{""age"":0,""nickname"":"""",""sex"":"""",""user_id"":0}},""sub_type"":""friend"",""target_id"":0,""time"":0,""user_id"":{userId}}}";
    }

    private Task RunCommand(Message message, string prefix, bool withBpRank = false)
    {
        var command = OsuCommandParser.parser(message.Command)?.Value;

        if (command == null)
        {
            message.Reply("错误的命令格式");
            return Task.CompletedTask;
        }

        if (command.BpRank != null && !withBpRank)
        {
            message.Reply("错误的命令格式");
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            var db = new BotDbContext();
            var at = message.MessageChain?.Messages.FirstOrDefault(m => m.Type == MessageDataType.At) as MessageDataAt;
            var o = at == null
                ? db.OsuBinds.FirstOrDefault(o => o.UserId == message.Sender!.Id)
                : db.OsuBinds.FirstOrDefault(o => o.UserId == at.Target);

            if (o != null)
            {
                var mode = string.IsNullOrEmpty(o.GameMode) ? OsuApi.ModeList[0] : o.GameMode;

                command = new OsuCommandParser.OsuCommand(
                    o.OsuUserName, command.BpRank, command.Mode ?? OsuApi.ModeList.IndexOf(mode));
            }
            else
            {
                if (at != null) command = new OsuCommandParser.OsuCommand($"[CQ:at,qq={at.Target}]", command.BpRank, command.Mode);
            }
        }

        AddCommandToQueue(message, $"{prefix} {command}");
        return Task.CompletedTask;
    }
}