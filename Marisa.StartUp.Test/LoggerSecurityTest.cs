using Microsoft.Extensions.DependencyInjection;
using NLog;
using NLog.Config;
using NLog.Targets;
using NUnit.Framework;

namespace Marisa.StartUp.Test;

[NonParallelizable]
public class LoggerSecurityTest
{
    [TearDown]
    public void TearDown()
    {
        LogManager.Shutdown();
    }

    [Test]
    public void Hosting_Request_Info_Should_Drop_Query_While_Safe_Controls_Remain_Visible()
    {
        const string sentinel = "SECRET_SENTINEL";
        var services = new ServiceCollection();
        services.ConfigLogger();

        var memory = new MemoryTarget("SecurityTestMemory") { Layout = "${message}" };
        LogManager.Configuration.AddTarget(memory);
        LogManager.Configuration.LoggingRules.Add(
            new LoggingRule("*", LogLevel.Trace, LogLevel.Fatal, memory));
        LogManager.ReconfigExistingLoggers();

        var hosting = LogManager.GetLogger("Microsoft.AspNetCore.Hosting.Diagnostics");
        hosting.Info("Request starting GET /health?code={0}", sentinel);
        hosting.Warn("hosting warning control");
        LogManager.GetLogger("Marisa.StartUp.HttpAccess")
            .Info("HTTP GET /health responded 200 in 1.0 ms");
        LogManager.Flush();

        Assert.Multiple(() =>
        {
            Assert.That(memory.Logs, Has.None.Contains(sentinel));
            Assert.That(memory.Logs, Has.Some.Contains("hosting warning control"));
            Assert.That(memory.Logs, Has.Some.Contains("HTTP GET /health responded 200"));
        });
    }
}
