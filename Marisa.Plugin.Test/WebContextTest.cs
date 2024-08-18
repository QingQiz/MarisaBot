using System;
using System.Threading.Tasks;
using Marisa.BotDriver.Extension;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class WebContextTest
{
    [Test]
    [Repeat(20)]
    public void Test1()
    {
        var tasks = new Task[20];

        for (var i = 0; i < 20; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                var context = new WebContext();

                context.Put("key", "value");

                await W1(context.Id);
            });
        }

        Task.WaitAll(tasks);
    }

    private static async Task W1(Guid contextId)
    {
        _ = Task.Run(() => W2(contextId));
        await Task.Delay(100);
    }

    private static void W2(Guid contextId)
    {
        Assert.IsTrue(WebContext.Get(contextId, "key") is string);
    }
}