using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Marisa.BotDriver.DI;
using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Entity.MessageSender;
using Marisa.BotDriver.Plugin;
using Microsoft.Extensions.DependencyInjection;
using MessageDataText = Marisa.BotDriver.Entity.MessageData.MessageDataText;

namespace Marisa.Plugin.Test;

public class TestBackend(
    IServiceProvider serviceProvider,
    IEnumerable<MarisaPluginBase> pluginsAll,
    DictionaryProvider dict,
    MessageSenderProvider messageSenderProvider,
    MessageQueueProvider messageQueueProvider) : BotDriver.BotDriver(serviceProvider, pluginsAll, dict, messageSenderProvider, messageQueueProvider)
{
    protected override Task RecvMessage()
    {
        return Task.CompletedTask;
    }

    protected override Task SendMessage()
    {
        return Task.CompletedTask;
    }

    protected override async Task ProcMessage()
    {
        while (true)
        {
            await Task.Delay(1000);
        }
        // ReSharper disable once FunctionNeverReturns
    }

    private static int _id;

    public async Task SetMessage(long senderId, long? groupId, string data)
    {
        var mid = new MessageDataId(_id++, (new DateTime().Ticks - 621355968000000000) / 10000000);

        var m = new Message(new MessageChain(mid, new MessageDataText(data)), MessageSenderProvider)
        {
            Sender    = new SenderInfo(senderId, ""),
            GroupInfo = groupId is null ? null : new GroupInfo((long)groupId, "", null),
            Type      = groupId is null ? MessageType.FriendMessage : MessageType.GroupMessage
        };
        await MessageQueueProvider.RecvQueue.Writer.WriteAsync(m);
    }

    public async Task<List<MessageToSend>> GetAllSend()
    {
        var res = new List<MessageToSend>();
        while (await MessageQueueProvider.SendQueue.Reader.WaitToReadAsync())
        {
            res.Add(await MessageQueueProvider.SendQueue.Reader.ReadAsync());
        }
        return res;
    }

    public async Task ProcAll()
    {
        var tasks = new List<Task>();

        var wait = 0;
        while (await MessageQueueProvider.RecvQueue.Reader.WaitToReadAsync())
        {
            var wait1 = wait;
            var m     = await MessageQueueProvider.RecvQueue.Reader.ReadAsync();
            tasks.Add(Task.Run(async () =>
            {
                await Task.Delay(wait1 * TimeSpan.FromSeconds(2));
                await ProcMessageStep(m);
            }));
            wait += 1;
        }
        await Task.WhenAll(tasks);
        MessageQueueProvider.SendQueue.Writer.Complete();
    }

    public void Finish()
    {
        MessageQueueProvider.RecvQueue.Writer.Complete();
    }

    public static TestBackend Create(params Type[] plugin)
    {
        var sc = Config(plugin);
        sc.AddScoped(typeof(BotDriver.BotDriver), typeof(TestBackend));

        var sp = sc.BuildServiceProvider();

        sp.GetService<DictionaryProvider>()!.Add("QQ", 642191352);

        return (sp.GetService<BotDriver.BotDriver>()! as TestBackend)!;
    }
}