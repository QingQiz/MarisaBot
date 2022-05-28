using System.Net.WebSockets;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using log4net;
using Marisa.EntityFrameworkCore;
using Marisa.Plugin.Shared.FSharp.Osu;
using Marisa.Plugin.Shared.Osu;
using Websocket.Client;

namespace Marisa.Plugin.Osu;

[MarisaPluginCommand("osu!", "osu", "!", "！")]
public partial class Osu : MarisaPluginBase
{
    private readonly WebsocketClient _wsClient;
    private readonly ILog _logger;
    private readonly Channel<(long Id, string Recv)> _recvQueue = Channel.CreateUnbounded<(long, string)>();

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
        var regex = new Regex(@"""user_id"":(\d*)");

        _wsClient.ReconnectionHappened.Subscribe(_ => { _logger.Warn("Reconnect to 猫猫"); });

        async void OnNext(ResponseMessage next)
        {
            var t  = next.Text;
            var id = regex.Match(t).Groups[1].Value;
            await _recvQueue.Writer.WriteAsync((long.Parse(id), t));
        }

        _wsClient.MessageReceived.Subscribe(OnNext);

        await _wsClient.Start();

        var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        var s = BuildCommand("heartbeat", 114514);

        while (await timer.WaitForNextTickAsync())
        {
            _wsClient.Send(s);
        }
    }

    private async Task<string> GetReplyByUserId(long userId)
    {
        var regex    = new Regex(@"""message"":""(.*?)""");
        var recvList = new List<(long, string)>();
        var res      = "";

        while (await _recvQueue.Reader.WaitToReadAsync())
        {
            var recv = await _recvQueue.Reader.ReadAsync();
            if (recv.Id == userId)
            {
                res = regex.Match(recv.Recv).Groups[1].Value;
                break;
            }

            recvList.Add(recv);
        }

        foreach (var recv in recvList)
        {
            await _recvQueue.Writer.WriteAsync(recv);
        }

        return res;
    }

    private async Task ReplyMessageByCommand(Message message, string command)
    {
        var cmd = BuildCommand(command, message.Sender!.Id);
        _wsClient.Send(cmd);

        var reply = await GetReplyByUserId(message.Sender!.Id);

        if (string.IsNullOrEmpty(reply))
        {
            message.Reply("猫猫不理魔理沙！");
            return;
        }

        if (reply.StartsWith("[CQ:image"))
        {
            var regex = new Regex(@"\[CQ:image,file=base64://(.*?)\]");
            message.Reply(MessageDataImage.FromBase64(regex.Match(reply).Groups[1].Value));
        }
        else
        {
            message.Reply(reply.Replace("猫猫", "魔理沙问猫猫，她说她"));
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

    private async Task RunCommand(Message message, string prefix, bool withBpRank=false)
    {
        var command = OsuCommandParser.parser(message.Command)?.Value;

        if (command == null)
        {
            message.Reply("错误的命令格式");
            return;
        }

        if (command.BpRank != null && !withBpRank)
        {
            message.Reply("错误的命令格式");
            return;
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
                command = new OsuCommandParser.OsuCommand(
                    o.OsuUserName, command.BpRank, command.Mode ?? OsuApi.ModeList.IndexOf(o.GameMode));
            }
            else
            {
                if (at != null)
                    command = new OsuCommandParser.OsuCommand($"[CQ:at,qq={at.Target}]", command.BpRank, command.Mode);
            }
        }
        await ReplyMessageByCommand(message, $"{prefix} {command}");
    }
}