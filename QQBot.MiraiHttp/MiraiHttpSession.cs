using Flurl;
using Flurl.Http;
using QQBot.EntityFrameworkCore;
using QQBot.EntityFrameworkCore.Entity.Audit;
using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Plugin;

namespace QQBot.MiraiHttp
{
    public partial class MiraiHttpSession
    {
        private readonly string _serverAddress;
        private readonly string _authKey;
        private readonly IEnumerable<MiraiPluginBase> _plugins;
        
        public MiraiHttpSession(DictionaryProvider dict, IEnumerable<MiraiPluginBase> plugins)
        {
            _serverAddress = dict["ServerAddress"];
            Id             = dict["QQ"];
            _authKey       = dict["AuthKey"];
            _plugins       = plugins;

            foreach (var plugin in _plugins)
            {
                AddPlugin(plugin);
            }
        }

        private string _session = null!;

        private delegate Task MessageHandler(MiraiHttpSession session, Message message, MiraiMessageType type,
            ref MiraiPluginTaskState state);

        private delegate Task EventHandler(MiraiHttpSession session, dynamic message, ref MiraiPluginTaskState state);

        private event MessageHandler OnMessage = null!;
        private event EventHandler OnEvent = null!;

        private static void CheckResponse(dynamic response)
        {
            if (response.code != 0) throw new Exception($"[Code {response.code}] {response.msg}");
        }

        public long Id { get; }

        public void AddPlugin(MiraiPluginBase miraiPlugin)
        {
            OnMessage += miraiPlugin.MessageHandlerWrapper;
            OnEvent   += miraiPlugin.EventHandlerWrapper;
        }

        public async Task Init()
        {
            // get session
            var login = await (await $"{_serverAddress}/verify".PostJsonAsync(new
                {
                    verifyKey = _authKey
                }))
                .GetJsonAsync();
            CheckResponse(login);

            _session = login.session;

            CheckResponse(await
                (await $"{_serverAddress}/bind".PostJsonAsync(new { sessionKey = _session, qq = Id }))
                .GetJsonAsync());
        }

        public async Task Run()
        {
            var reportAddress = $"{_serverAddress}/countMessage";
            while (true)
            {
                var msgCnt = await reportAddress.SetQueryParam("sessionKey", _session).GetJsonAsync();
                CheckResponse(msgCnt);

                if (msgCnt.data == 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                    continue;
                }

                var msg = await $"{_serverAddress}/fetchMessage"
                    .SetQueryParams(new
                    {
                        sessionKey = _session,
                        count      = msgCnt.data
                    }).GetJsonAsync();
                CheckResponse(msg);

                var tasks = ((List<dynamic>)msg.data).Where(m => m.type.Contains("Message")).Select(async m =>
                {
                    var log = new AuditLog
                    {
                        EventId   = Guid.NewGuid(),
                        EventType = m.type,
                        Message   = Newtonsoft.Json.JsonConvert.SerializeObject(m.messageChain),
                        Time      = DateTime.Now
                    };

                    var message = new Message
                    {
                        MessageChain = new MessageChain(m.messageChain)
                    };

                    var mType  = MiraiMessageType.FriendMessage;
                    var tState = MiraiPluginTaskState.NoResponse;

                    switch (m.type)
                    {
                        case "FriendMessage":
                            message.GroupInfo = null;
                            message.Sender    = new MessageSenderInfo(m.sender.id, m.sender.nickname, m.sender.remark);

                            log.UserId    = message.Sender.Id.ToString();
                            log.UserName  = message.Sender.Name;
                            log.UserAlias = message.Sender.Remark;

                            break;
                        case "StrangerMessage":
                            message.GroupInfo = null;
                            message.Sender    = new MessageSenderInfo(m.sender.id, m.sender.nickname, m.sender.remark);

                            log.UserId    = message.Sender.Id.ToString();
                            log.UserName  = message.Sender.Name;
                            log.UserAlias = message.Sender.Remark;

                            mType = MiraiMessageType.StrangerMessage;
                            break;
                        case "GroupMessage":
                            message.GroupInfo = new GroupInfo(m.sender.group.id, m.sender.group.name,
                                m.sender.group.permission);
                            message.Sender = new MessageSenderInfo(m.sender.id, m.sender.memberName,
                                permission: m.sender.permission);

                            log.UserId    = message.Sender.Id.ToString();
                            log.UserName  = message.Sender.Name;
                            log.UserAlias = message.Sender.Remark;
                            log.GroupName = message.GroupInfo?.Name;
                            log.GroupId   = message.GroupInfo?.Id.ToString();

                            mType = MiraiMessageType.GroupMessage;
                            break;
                        case "TempMessage":
                            message.GroupInfo = new GroupInfo(m.sender.group.id, m.sender.group.name,
                                m.sender.group.permission);
                            message.Sender = new MessageSenderInfo(m.sender.id, m.sender.memberName,
                                permission: m.sender.permission);

                            log.UserId    = message.Sender.Id.ToString();
                            log.UserName  = message.Sender.Name;
                            log.UserAlias = message.Sender.Remark;
                            log.GroupName = message.GroupInfo?.Name;
                            log.GroupId   = message.GroupInfo?.Id.ToString();

                            mType = MiraiMessageType.TempMessage;
                            break;
                    }

                    await using var dbContext = new BotDbContext();
                    await dbContext.Logs.AddAsync(log);
                    await dbContext.SaveChangesAsync();

                    Console.WriteLine(log.Message.Length < 120
                        ? $"[{DateTime.Now:yyyy-MM-dd hh:mm:ss}][{m.type.PadLeft(15)}] {log.Message}"
                        : $"[{DateTime.Now:yyyy-MM-dd hh:mm:ss}][{m.type.PadLeft(15)}] {log.Message[..120]}...");

                    await OnMessage.Invoke(this, message, mType, ref tState);
                });
                await Task.WhenAll(tasks);

                tasks = ((List<dynamic>)msg.data).Where(m => m.type.Contains("Event")).Select(async m =>
                {
                    var log = new AuditLog
                    {
                        EventId   = Guid.NewGuid(),
                        EventType = m.type,
                        Message   = Newtonsoft.Json.JsonConvert.SerializeObject(m),
                        Time      = DateTime.Now
                    };

                    var tState = MiraiPluginTaskState.NoResponse;

                    await using var dbContext = new BotDbContext();
                    await dbContext.Logs.AddAsync(log);
                    await dbContext.SaveChangesAsync();

                    Console.WriteLine(log.Message.Length < 120
                        ? $"[{DateTime.Now:yyyy-MM-dd hh:mm:ss}][{m.type.PadLeft(15)}] {log.Message}"
                        : $"[{DateTime.Now:yyyy-MM-dd hh:mm:ss}][{m.type.PadLeft(15)}] {log.Message[..120]}...");

                    await OnEvent.Invoke(this, m, ref tState);
                });

                await Task.WhenAll(tasks);
            }
            // ReSharper disable once FunctionNeverReturns
        }
    }
}