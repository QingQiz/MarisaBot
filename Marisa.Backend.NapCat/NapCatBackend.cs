using System.Collections.Concurrent;
using System.ComponentModel;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Marisa.BotDriver.DI;
using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Entity.MessageSender;
using Marisa.BotDriver.Plugin;
using Marisa.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace Marisa.Backend.NapCat;

public class NapCatBackend : BotDriver.BotDriver
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null
    };

    private readonly Logger _logger;
    private readonly DictionaryProvider _dict;
    private readonly Uri _endpoint;
    private readonly NapCatConfiguration _config;
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pendingActions = new();
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private ClientWebSocket? _socket;
    private Task? _receiveTask;
    private long _echo;
    private long _selfId;

    public NapCatBackend(
        IServiceProvider serviceProvider,
        IEnumerable<MarisaPluginBase> pluginsAll,
        DictionaryProvider dict,
        MessageSenderProvider messageSenderProvider,
        MessageQueueProvider messageQueueProvider
    ) : base(serviceProvider, pluginsAll, dict, messageSenderProvider, messageQueueProvider)
    {
        _logger   = LogManager.GetCurrentClassLogger();
        _dict     = dict;
        _config   = ConfigurationManager.Configuration.NapCat;
        _endpoint = BuildEndpoint(string.IsNullOrWhiteSpace(_config.Endpoint) ? "ws://127.0.0.1:3001" : _config.Endpoint, _config.Token);

        if (long.TryParse(_config.SelfId, out var selfId))
        {
            SetSelfId(selfId);
        }
    }

    public new static IServiceCollection Config(Type[] types)
    {
        var sc = BotDriver.BotDriver.Config(types);
        sc.AddScoped<BotDriver.BotDriver, NapCatBackend>();
        return sc;
    }

    protected override Task RecvMessage()
    {
        return _receiveTask ?? Task.CompletedTask;
    }

    protected override async Task SendMessage()
    {
        var taskList = new List<Task>();

        try
        {
            while (await MessageQueueProvider.SendQueue.Reader.WaitToReadAsync(ShutdownToken))
            {
                var s = await MessageQueueProvider.SendQueue.Reader.ReadAsync(ShutdownToken);

                switch (s.Type)
                {
                    case MessageType.GroupMessage:
                        taskList.Add(Task.Run(() => SendGroupMessage(s.MessageChain, s.ReceiverId, s.QuoteId), ShutdownToken));
                        break;
                    case MessageType.FriendMessage:
                        taskList.Add(Task.Run(() => SendFriendMessage(s.MessageChain, s.ReceiverId, s.QuoteId), ShutdownToken));
                        break;
                    case MessageType.TempMessage:
                        taskList.Add(Task.Run(() => SendTempMessage(s.MessageChain, s.ReceiverId, s.GroupId, s.QuoteId), ShutdownToken));
                        break;
                    case MessageType.StrangerMessage:
                        taskList.Add(Task.Run(() => SendFriendMessage(s.MessageChain, s.ReceiverId, s.QuoteId), ShutdownToken));
                        break;
                    default:
                        throw new InvalidEnumArgumentException();
                }

                if (taskList.Count < 100) continue;

                await Task.WhenAll(taskList);
                taskList.Clear();
            }
        }
        catch (OperationCanceledException) when (ShutdownToken.IsCancellationRequested)
        {
        }

        await Task.WhenAll(taskList);
        return;

        async Task SendGroupMessage(MessageChain message, long target, long? quote = null)
        {
            _logger.Info($"{target} <= {message}");
            await SendAction("send_group_msg", new Dictionary<string, object?>
            {
                ["group_id"] = target.ToString(),
                ["message"] = ConstructSegments(quote, message)
            });
        }

        async Task SendFriendMessage(MessageChain message, long target, long? quote = null)
        {
            _logger.Info($"{target} <- {message}");
            await SendAction("send_private_msg", new Dictionary<string, object?>
            {
                ["user_id"] = target.ToString(),
                ["message"] = ConstructSegments(quote, message)
            });
        }

        async Task SendTempMessage(MessageChain message, long target, long? groupId, long? quote = null)
        {
            _logger.Info($"{target} <-[temp:{groupId}] {message}");

            // OneBot keeps temporary sessions on send_private_msg with an optional group_id context.
            // NapCat accepts this shape for private.group events reported from group temporary sessions.
            var parameters = new Dictionary<string, object?>
            {
                ["user_id"] = target.ToString(),
                ["message"] = ConstructSegments(quote, message)
            };

            if (groupId is not null)
            {
                parameters["group_id"] = groupId.Value.ToString();
            }

            await SendAction("send_private_msg", parameters);
        }
    }

    public override async Task Invoke()
    {
        await ConnectWithRetry();
        _receiveTask = Task.Run(ReceiveLoop, ShutdownToken);
        await InitializeSelfInfo();
        await base.Invoke();
    }

    public override void Stop()
    {
        base.Stop();
        FailPendingActions(new OperationCanceledException("Application is shutting down", ShutdownToken));
        CloseSocket();
    }

    private async Task InitializeSelfInfo()
    {
        try
        {
            var data = await SendAction("get_login_info", new Dictionary<string, object?>());
            var selfId = ReadLong(data, "user_id");
            if (selfId != 0)
            {
                SetSelfId(selfId);
            }
        }
        catch (Exception ex)
        {
            _logger.Warn(ex, "Failed to initialize NapCat login info; set napCat.selfId if @ triggers are needed before the first message");
        }
    }

    private async Task ReceiveLoop()
    {
        while (!ShutdownToken.IsCancellationRequested)
        {
            try
            {
                await ConnectWithRetry();
                var socket = _socket!;
                var json = await ReceiveText(socket);
                if (json is null)
                {
                    CloseSocket();
                    continue;
                }

                using var document = JsonDocument.Parse(json);
                var root = document.RootElement.Clone();

                if (TryCompletePendingAction(root))
                {
                    continue;
                }

                await HandleEvent(root);
            }
            catch (OperationCanceledException) when (ShutdownToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "NapCat WebSocket receive loop failed; reconnecting in 5 seconds");
                FailPendingActions(ex);
                CloseSocket();
                await Task.Delay(TimeSpan.FromSeconds(5), ShutdownToken);
            }
        }
    }

    private async Task HandleEvent(JsonElement root)
    {
        if (_selfId == 0)
        {
            var selfId = ReadLong(root, "self_id");
            if (selfId != 0)
            {
                SetSelfId(selfId);
            }
        }

        var postType = ReadString(root, "post_type");

        switch (postType)
        {
            case "message":
            {
                var message = ConvertMessageEvent(root);
                if (message is not null)
                {
                    await MessageQueueProvider.RecvQueue.Writer.WriteAsync(message, ShutdownToken);
                }

                break;
            }
            case "notice":
            {
                var message = ConvertNoticeEvent(root);
                if (message is not null)
                {
                    await MessageQueueProvider.RecvQueue.Writer.WriteAsync(message, ShutdownToken);
                }

                break;
            }
            case "request":
            {
                var message = ConvertRequestEvent(root);
                if (message is not null)
                {
                    await MessageQueueProvider.RecvQueue.Writer.WriteAsync(message, ShutdownToken);
                }

                break;
            }
            case "message_sent":
            {
                var message = ConvertMessageEvent(root, isSentBySelf: true);
                if (message is not null)
                {
                    await MessageQueueProvider.RecvQueue.Writer.WriteAsync(message, ShutdownToken);
                }

                break;
            }
            case "meta_event":
            {
                var message = ConvertMetaEvent(root);
                if (message is not null)
                {
                    await MessageQueueProvider.RecvQueue.Writer.WriteAsync(message, ShutdownToken);
                }

                break;
            }
            default:
                if (postType is not null)
                {
                    _logger.Debug($"Not implemented NapCat event: `{root.GetRawText()}`");
                }

                break;
        }
    }

    private Message? ConvertMessageEvent(JsonElement root, bool isSentBySelf = false)
    {
        var senderId = ReadLong(root, "user_id");
        if (!isSentBySelf && senderId == _selfId) return null;

        var messageType = ReadString(root, "message_type");
        if (messageType is not ("group" or "private")) return null;
        var subType = ReadString(root, "sub_type");

        var ds = new List<MessageData>
        {
            new MessageDataId(ReadLong(root, "message_id"), ReadLong(root, "time"))
        };

        if (!root.TryGetProperty("message", out var rawMessage)) return null;

        if (rawMessage.ValueKind == JsonValueKind.String)
        {
            ds.Add(new MessageDataText(rawMessage.GetString() ?? string.Empty));
        }
        else if (rawMessage.ValueKind == JsonValueKind.Array)
        {
            foreach (var segment in rawMessage.EnumerateArray())
            {
                var converted = ConvertSegment(segment);
                if (converted is null) return null;
                if (converted.Type != MessageDataType.Unknown)
                {
                    ds.Add(converted);
                }
            }
        }
        else
        {
            return null;
        }

        var sender = root.TryGetProperty("sender", out var senderElement) ? senderElement : default;
        var senderName = ReadString(sender, "card") ?? ReadString(sender, "nickname") ?? string.Empty;
        var role = ReadString(sender, "role") ?? string.Empty;

        var groupId = ReadLong(root, "group_id");
        var targetId = ReadLong(root, "target_id");

        return new Message(MessageSenderProvider, ds.ToArray())
        {
            GroupInfo = messageType == "group"
                ? new GroupInfo(groupId, ReadString(root, "group_name") ?? string.Empty, role)
                : null,
            Sender = new SenderInfo(senderId, senderName, Permission: role),
            Type   = messageType == "group" ? MessageType.GroupMessage : PrivateMessageType(subType, targetId)
        };
    }

    private static MessageType PrivateMessageType(string? subType, long targetId)
    {
        return subType switch
        {
            // NapCat reports temporary private sessions as private.group, but downstream dialog and private-command
            // handling expects these follow-up messages to behave like normal friend messages.
            "group" => MessageType.FriendMessage,
            "other" => MessageType.StrangerMessage,
            _ => MessageType.FriendMessage
        };
    }

    private MessageData? ConvertSegment(JsonElement segment)
    {
        var type = ReadString(segment, "type");
        if (!segment.TryGetProperty("data", out var data)) data = default;

        switch (type)
        {
            case "text":
                return new MessageDataText(ReadString(data, "text") ?? string.Empty);
            case "face":
                return OneBotSegment("face", data, MessageDataType.Face);
            case "image":
                return ConvertImageSegment(data);
            case "record":
                return ConvertRecordSegment(data);
            case "video":
                return OneBotSegment("video", data, MessageDataType.Video);
            case "at":
            {
                var qq = ReadString(data, "qq");
                if (qq == "all") return OneBotSegment("at", data, MessageDataType.AtAll);
                return long.TryParse(qq, out var target) ? new MessageDataAt(target) : null;
            }
            case "rps":
                return OneBotSegment("rps", data, MessageDataType.Rps);
            case "dice":
                return OneBotSegment("dice", data, MessageDataType.Dice);
            case "shake":
                return OneBotSegment("shake", data, MessageDataType.Shake);
            case "poke":
                return OneBotSegment("poke", data, MessageDataType.Poke);
            case "share":
                return OneBotSegment("share", data, MessageDataType.Share);
            case "contact":
                return OneBotSegment("contact", data, MessageDataType.Contact);
            case "location":
                return OneBotSegment("location", data, MessageDataType.Location);
            case "music":
                return OneBotSegment("music", data, MessageDataType.MusicShare);
            case "reply":
                return OneBotSegment("reply", data, MessageDataType.Quote);
            case "forward":
                return OneBotSegment("forward", data, MessageDataType.Forward);
            case "node":
                return OneBotSegment("node", data, MessageDataType.Node);
            case "json":
                return OneBotSegment("json", data, MessageDataType.Json);
            case "mface":
                return OneBotSegment("mface", data, MessageDataType.MFace);
            case "file":
                return OneBotSegment("file", data, MessageDataType.File);
            case "markdown":
                return OneBotSegment("markdown", data, MessageDataType.Markdown);
            case "lightapp":
                return OneBotSegment("lightapp", data, MessageDataType.LightApp);
            default:
                return null;
        }
    }

    private static MessageDataImage ConvertImageSegment(JsonElement data)
    {
        return new MessageDataImage
        {
            File = ReadString(data, "file"),
            ImageId = ReadString(data, "file_id"),
            Url = ReadString(data, "url"),
            Path = ReadString(data, "path"),
            Name = ReadString(data, "name"),
            Summary = ReadString(data, "summary"),
            SubType = ReadString(data, "sub_type"),
            FileSize = ReadString(data, "file_size"),
            FileUnique = ReadString(data, "file_unique")
        };
    }

    private static MessageDataVoice ConvertRecordSegment(JsonElement data)
    {
        return new MessageDataVoice
        {
            File = ReadString(data, "file"),
            VoiceId = ReadString(data, "file_id"),
            Url = ReadString(data, "url"),
            Path = ReadString(data, "path"),
            Name = ReadString(data, "name"),
            FileSize = ReadString(data, "file_size"),
            FileUnique = ReadString(data, "file_unique")
        };
    }

    private static MessageDataOneBotSegment OneBotSegment(string segmentType, JsonElement data, MessageDataType type)
    {
        return new MessageDataOneBotSegment(segmentType, ToDictionary(data), type);
    }

    private Message? ConvertNoticeEvent(JsonElement root)
    {
        var noticeType = ReadString(root, "notice_type");
        var groupId = ReadLong(root, "group_id");
        var userId = ReadLong(root, "user_id");
        var operatorId = ReadLong(root, "operator_id");

        return noticeType switch
        {
            "group_ban" when IsBotBanNotice(userId) && ReadString(root, "sub_type") == "ban" => GroupNotice(root,
                new MessageDataBotMute(groupId, BotMuteDuration(root)), operatorId),
            "group_ban" when IsBotBanNotice(userId) && ReadString(root, "sub_type") == "lift_ban" => GroupNotice(root,
                new MessageDataBotUnmute(groupId), operatorId),
            "group_decrease" => GroupNotice(root, new MessageDataMemberLeave(userId, string.Empty, operatorId == 0 ? null : operatorId)),
            "group_increase" => GroupNotice(root, new MessageDataNewMember(userId, groupId,
                ReadString(root, "sub_type") == "approve" || operatorId == 0 ? null : operatorId)),
            "notify" when ReadString(root, "sub_type") == "poke" => PokeNotice(root),
            "bot_online" => new Message(MessageSenderProvider, new MessageDataBotOnline())
            {
                Sender = new SenderInfo(userId, string.Empty),
                Type = MessageType.FriendMessage
            },
            "bot_offline" => new Message(MessageSenderProvider, new MessageDataBotOffline())
            {
                Sender = new SenderInfo(userId, string.Empty),
                Type = MessageType.FriendMessage
            },
            _ => null
        };

        bool IsBotBanNotice(long id) => id == 0 || id == _selfId;

        long? BotMuteDuration(JsonElement data)
        {
            var duration = ReadLong(data, "duration");
            return duration <= 0 ? null : duration;
        }
    }

    private Message? ConvertRequestEvent(JsonElement root)
    {
        return null;
    }

    private Message? ConvertMetaEvent(JsonElement root)
    {
        if (ReadString(root, "meta_event_type") == "lifecycle" && ReadString(root, "sub_type") is "connect" or "enable")
        {
            return new Message(MessageSenderProvider, new MessageDataBotOnline())
            {
                Sender = new SenderInfo(_selfId, string.Empty),
                Type = MessageType.FriendMessage
            };
        }

        return null;
    }

    private Message GroupNotice(JsonElement root, MessageData data, long? senderId = null)
    {
        var groupId = ReadLong(root, "group_id");
        var userId = senderId ?? ReadLong(root, "user_id");

        return new Message(MessageSenderProvider, data)
        {
            GroupInfo = new GroupInfo(groupId, string.Empty, string.Empty),
            Sender = new SenderInfo(userId, string.Empty),
            Type = MessageType.GroupMessage
        };
    }

    private Message? PokeNotice(JsonElement root)
    {
        var groupId = ReadLong(root, "group_id");
        var userId = ReadLong(root, "user_id");
        var targetId = ReadLong(root, "target_id");

        if (targetId == 0 || userId == 0) return null;

        return new Message(MessageSenderProvider, new MessageDataNudge(targetId, userId))
        {
            GroupInfo = groupId == 0 ? null : new GroupInfo(groupId, string.Empty, string.Empty),
            Sender = new SenderInfo(userId, string.Empty),
            Type = groupId == 0 ? MessageType.FriendMessage : MessageType.GroupMessage
        };
    }

    private static object[] ConstructSegments(long? quote, MessageChain message)
    {
        var segments = new List<object>();

        if (quote is not null)
        {
            segments.Add(Segment("reply", new Dictionary<string, object?>
            {
                ["id"] = quote.Value.ToString()
            }));
        }

        foreach (var m in message.Messages)
        {
            switch (m)
            {
                case MessageDataText text:
                    segments.Add(Segment("text", new Dictionary<string, object?>
                    {
                        ["text"] = text.Text.ToString()
                    }));
                    break;
                case MessageDataImage image:
                    segments.Add(Segment("image", new Dictionary<string, object?>
                    {
                        ["file"] = ImageFile(image),
                        ["name"] = image.Name,
                        ["summary"] = image.Summary,
                        ["sub_type"] = image.SubType
                    }));
                    break;
                case MessageDataVoice voice:
                    segments.Add(Segment("record", new Dictionary<string, object?>
                    {
                        ["file"] = VoiceFile(voice),
                        ["name"] = voice.Name
                    }));
                    break;
                case MessageDataAt at:
                    segments.Add(Segment("at", new Dictionary<string, object?>
                    {
                        ["qq"] = at.Target.ToString()
                    }));
                    break;
                case MessageDataOneBotSegment oneBotSegment:
                    // Pass through exact NapCat/OneBot segments for types Marisa does not model directly.
                    segments.Add(Segment(oneBotSegment.SegmentType, oneBotSegment.Data));
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        return segments.ToArray();

        static object Segment(string type, Dictionary<string, object?> data)
        {
            return new Dictionary<string, object?>
            {
                ["type"] = type,
                ["data"] = data.Where(x => x.Value is not null).ToDictionary(x => x.Key, x => x.Value)
            };
        }

        static string ImageFile(MessageDataImage image)
        {
            if (image.Base64 is not null)
            {
                return image.Base64.StartsWith("base64://") || image.Base64.StartsWith("data:")
                    ? image.Base64
                    : $"base64://{image.Base64}";
            }

            return image.File ?? image.Path ?? image.Url ?? throw new InvalidOperationException("Image message has no File, Base64, Path, or Url");
        }

        static string VoiceFile(MessageDataVoice voice)
        {
            if (voice.Base64 is not null)
            {
                return voice.Base64.StartsWith("base64://") || voice.Base64.StartsWith("data:")
                    ? voice.Base64
                    : $"base64://{voice.Base64}";
            }

            return voice.File ?? voice.Path ?? voice.Url ?? throw new InvalidOperationException("Voice message has no File, Base64, Path, or Url");
        }
    }

    private async Task<JsonElement> SendAction(string action, Dictionary<string, object?> parameters)
    {
        await ConnectWithRetry();

        var echo = Interlocked.Increment(ref _echo).ToString();
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingActions[echo] = tcs;

        var request = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["action"] = action,
            ["params"] = parameters,
            ["echo"] = echo
        }, JsonOptions);

        try
        {
            await SendText(request);
            var response = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(30), ShutdownToken);

            if (ReadString(response, "status") != "ok" || ReadLong(response, "retcode", 0) != 0)
            {
                throw new InvalidOperationException($"NapCat action `{action}` failed: {response.GetRawText()}");
            }

            return response.TryGetProperty("data", out var data) ? data.Clone() : default;
        }
        finally
        {
            _pendingActions.TryRemove(echo, out _);
        }
    }

    private async Task SendText(string text)
    {
        var socket = _socket;
        if (socket is null || socket.State != WebSocketState.Open)
        {
            throw new WebSocketException("NapCat WebSocket is not connected");
        }

        var bytes = Encoding.UTF8.GetBytes(text);
        await _sendLock.WaitAsync(ShutdownToken);
        try
        {
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ShutdownToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task<string?> ReceiveText(ClientWebSocket socket)
    {
        var buffer = new byte[8192];
        using var stream = new MemoryStream();
        WebSocketReceiveResult result;

        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ShutdownToken);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            stream.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        return result.MessageType == WebSocketMessageType.Text
            ? Encoding.UTF8.GetString(stream.ToArray())
            : null;
    }

    private bool TryCompletePendingAction(JsonElement root)
    {
        var echo = ReadString(root, "echo");
        if (echo is null || !_pendingActions.TryRemove(echo, out var tcs)) return false;

        tcs.TrySetResult(root);
        return true;
    }

    private void FailPendingActions(Exception ex)
    {
        foreach (var (echo, tcs) in _pendingActions)
        {
            if (_pendingActions.TryRemove(echo, out _))
            {
                tcs.TrySetException(ex);
            }
        }
    }

    private async Task ConnectWithRetry()
    {
        await _connectLock.WaitAsync(ShutdownToken);
        try
        {
            if (_socket?.State == WebSocketState.Open) return;

            while (!ShutdownToken.IsCancellationRequested)
            {
                try
                {
                    CloseSocket();

                    var socket = new ClientWebSocket();
                    if (!string.IsNullOrEmpty(_config.Token))
                    {
                        socket.Options.SetRequestHeader("Authorization", $"Bearer {_config.Token}");
                    }

                    await socket.ConnectAsync(_endpoint, ShutdownToken);
                    _socket = socket;
                    _logger.Info($"Connected to NapCat OneBot WebSocket: {_endpoint}");
                    return;
                }
                catch (Exception ex)
                {
                    _logger.Warn(ex, $"Failed to connect NapCat OneBot WebSocket {_endpoint}; retrying in 5 seconds");
                    await Task.Delay(TimeSpan.FromSeconds(5), ShutdownToken);
                }
            }

            throw new OperationCanceledException(ShutdownToken);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private void CloseSocket()
    {
        try
        {
            _socket?.Abort();
            _socket?.Dispose();
        }
        catch
        {
            // Ignore cleanup failures while reconnecting.
        }
        finally
        {
            _socket = null;
        }
    }

    private void SetSelfId(long selfId)
    {
        _selfId = selfId;
        _dict["QQ"] = selfId;
    }

    private static Uri BuildEndpoint(string endpoint, string? token)
    {
        if (string.IsNullOrEmpty(token)) return new Uri(endpoint);

        var separator = endpoint.Contains('?') ? '&' : '?';
        return new Uri($"{endpoint}{separator}access_token={Uri.EscapeDataString(token)}");
    }

    private static string? ReadString(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Undefined || !element.TryGetProperty(name, out var value)) return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static long ReadLong(JsonElement element, string name, long fallback = 0)
    {
        if (element.ValueKind == JsonValueKind.Undefined || !element.TryGetProperty(name, out var value)) return fallback;

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt64(out var number) => number,
            JsonValueKind.String when long.TryParse(value.GetString(), out var number) => number,
            _ => fallback
        };
    }

    private static long FirstNonZero(params long[] values)
    {
        return values.FirstOrDefault(x => x != 0);
    }

    private static Dictionary<string, object?> ToDictionary(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return new Dictionary<string, object?>();

        return element.EnumerateObject().ToDictionary(property => property.Name, property => ToObject(property.Value));
    }

    private static object? ToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => ToDictionary(element),
            JsonValueKind.Array => element.EnumerateArray().Select(ToObject).ToArray(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var value) => value,
            JsonValueKind.Number when element.TryGetDouble(out var value) => value,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.GetRawText()
        };
    }
}
