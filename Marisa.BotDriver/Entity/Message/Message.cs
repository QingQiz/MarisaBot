using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Entity.MessageSender;

namespace Marisa.BotDriver.Entity.Message;

public record Message(string Command)
{
    // Info
    public GroupInfo? GroupInfo;
    public SenderInfo? Sender;

    public readonly MessageChain? MessageChain;

    // Control
    public MessageType Type;
    private readonly MessageSenderProvider _sender;

    public Message(MessageChain chain, MessageSenderProvider sender) : this(chain.Text.Trim())
    {
        MessageChain = chain;
        _sender      = sender;
    }

    public Message(MessageSenderProvider sender, params MessageData.MessageData[] md)
        : this(new MessageChain(md), sender) {}

    public MessageDataId MessageId =>
        (MessageChain!.Messages.FirstOrDefault(m => m.Type == MessageDataType.Id) as MessageDataId)!;

    /// <summary>
    /// 判断消息是否 AT 某人
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    public bool IsAt(long target)
    {
        return MessageChain!.Messages.Any(m => m.Type == MessageDataType.At && (m as MessageDataAt)!.Target == target);
    }

    public IEnumerable<long> At()
    {
        return MessageChain!.Messages
            .Where(m => m.Type == MessageDataType.At)
            .Select(m => (m as MessageDataAt)!.Target);
    }

    /// <summary>
    /// 判断是否是纯文本消息
    /// </summary>
    /// <returns></returns>
    public bool IsPlainText()
    {
        return MessageChain!.Messages.All(m => m.Type is MessageDataType.Text or MessageDataType.Id) && // 确保所有消息都是纯文本
               MessageChain!.Messages.Any(m => m.Type == MessageDataType.Text);                         // 确保至少存在一条数据
    }

    /// <summary>
    /// 消息在哪，群或者私聊
    /// </summary>
    public long Location => Type switch
    {
        MessageType.GroupMessage    => GroupInfo!.Id,
        MessageType.FriendMessage   => Sender!.Id,
        MessageType.TempMessage     => Sender!.Id,
        MessageType.StrangerMessage => Sender!.Id,
        _                           => throw new ArgumentOutOfRangeException()
    };

    public override string ToString()
    {
        return GroupInfo == null
            ? $"[{Type}] {Sender?.Name ?? "Unknown"} ({Sender?.Id ?? 0}) => {MessageChain}"
            : $"[{Type}] {Sender?.Name ?? "Unknown"} ({Sender?.Id ?? 0}) -> {GroupInfo?.Name ?? "Unknown"} ({GroupInfo?.Id ?? 0}) => {MessageChain}";
    }

    #region Reply

    /// <summary>
    /// 通过接收到的消息发送复杂消息到接收处
    /// </summary>
    public void Reply(MessageChain message, bool quote = true)
    {
        _sender.Reply(message, this, quote && MessageChain!.CanBeReferenced).Wait();
    }

    public void Reply(MessageData.MessageData message, bool quote = true)
    {
        _sender.Reply(message, this, quote && MessageChain!.CanBeReferenced).Wait();
    }

    public void Reply(params MessageData.MessageData[] messages)
    {
        _sender.Reply(new MessageChain(messages), this, MessageChain!.CanBeReferenced).Wait();
    }

    public void Send(params MessageData.MessageData[] messages)
    {
        _sender.Reply(new MessageChain(messages), this, false).Wait();
    }

    /// <summary>
    /// 通过接收到的消息发送纯文本消息到接收处
    /// </summary>
    public void Reply(string text, bool quote = true) => Reply(MessageChain.FromText(text), quote);

    #endregion
}