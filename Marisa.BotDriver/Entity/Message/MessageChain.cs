﻿using Marisa.BotDriver.Entity.MessageData;

namespace Marisa.BotDriver.Entity.Message;

public record MessageChain
{
    public readonly List<MessageData.MessageData> Messages;

    public MessageChain(params MessageData.MessageData[] messages)
    {
        Messages = messages.ToList();
    }

    public MessageChain(IEnumerable<MessageData.MessageData> messages)
    {
        Messages = messages.ToList();
    }

    public bool CanBeReferenced => Messages.All(m => m.Type is not (
        MessageDataType.Voice or MessageDataType.Nudge or MessageDataType.NewMember or MessageDataType.MemberLeave
        )
    );

    public string Text => string.Join(' ',
        Messages.Where(m => m.Type == MessageDataType.Text).Select(m => (m as MessageDataText)!.Text)
    );

    public static MessageChain FromText(string text)
    {
        return new MessageChain(new MessageDataText(text.AsMemory()));
    }

    public static MessageChain FromImageB64(string b64)
    {
        return new MessageChain(MessageDataImage.FromBase64(b64));
    }

    public static MessageChain FromVoiceB64(string b64)
    {
        return new MessageChain(MessageDataVoice.FromBase64(b64));
    }

    public override string ToString()
    {
        return string.Join(' ', Messages.Select(m =>
        {
            return m.Type switch
            {
                MessageDataType.Text => (m as MessageDataText)!.Text.ToString(),
                _                    => $"[:{m.Type.ToString()}:]"
            };
        }));
    }
}