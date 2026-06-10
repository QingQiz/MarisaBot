using System.Diagnostics;
using System.Text;
using Marisa.Plugin.Shared.Dialog;
using Marisa.Plugin.Shared.Interface;

namespace Marisa.Plugin;

[MarisaPluginNoDoc]
[MarisaPlugin(PluginPriority.Command)]
[MarisaPluginCommand(":cmd")]
public class Command : MarisaPluginBase
{
    private static IEnumerable<long> Commander => ConfigurationManager.Configuration.Commander;

    /// <summary>
    ///     start an interactive shell
    /// </summary>
    /// <param name="m"></param>
    /// <returns></returns>
    [MarisaPluginNoDoc]
    [MarisaPluginCommand("shell")]
    private MarisaPluginTaskState Shell(Message m)
    {
        var dialogKey = (m.GroupInfo?.Id, m.Sender.Id);

        if (!Commander.Contains(m.Sender.Id))
        {
            m.Reply("你没资格啊，你没资格。正因如此，你没资格。");
            return MarisaPluginTaskState.CompletedTask;
        }

        const string promptMarker = "__MARISA_SHELL_READY__>";
        var idleTimeout = TimeSpan.FromMinutes(10);
        var gate = new object();
        ShellCommandState? activeCommand = null;
        var lastActivityAt = DateTime.UtcNow;
        var shellClosed = false;

        var proc = new Process
        {
            StartInfo = new ProcessStartInfo("cmd.exe")
            {
                Arguments              = $"/Q /K prompt {promptMarker[..^1]}$G & @echo off & cd /d \"{Environment.CurrentDirectory}\"",
                WorkingDirectory       = Environment.CurrentDirectory,
                UseShellExecute        = false,
                RedirectStandardInput  = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                StandardOutputEncoding = Console.OutputEncoding,
                StandardErrorEncoding  = Console.OutputEncoding
            }
        };

        proc.Start();

        _ = Task.Run(() => PumpStreamAsync(proc.StandardOutput));
        _ = Task.Run(() => PumpStreamAsync(proc.StandardError));
        _ = Task.Run(WatchShellTimeoutAsync);

        DialogManager.TryAddDialog(dialogKey, message =>
        {
            if (proc.HasExited)
            {
                message.Reply("Shell已经退出了");
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            }

            var command = message.Command.Trim().ToString();

            if (command is "退出" || command.Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                TryCloseShell();
                message.Reply("Shell退出了");
                return Task.FromResult(MarisaPluginTaskState.CompletedTask);
            }

            var commandState = new ShellCommandState();
            lock (gate)
            {
                activeCommand = commandState;
                lastActivityAt = DateTime.UtcNow;
            }

            proc.StandardInput.WriteLine(command);
            proc.StandardInput.Flush();

            _ = Task.Run(() => ObserveCommandAsync(message, commandState));

            return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
        }, this);

        m.Reply($"Shell启动了；发送“退出”可关闭，空闲 {idleTimeout.TotalMinutes:0} 分钟会自动退出。长输出会分段发送，若命令等待输入可继续直接发送内容。");

        return MarisaPluginTaskState.CompletedTask;

        async Task PumpStreamAsync(StreamReader reader)
        {
            var buffer = new char[256];

            while (!proc.HasExited)
            {
                var read = await reader.ReadAsync(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    break;
                }

                AppendOutput(new string(buffer, 0, read));
            }
        }

        void AppendOutput(string text)
        {
            lock (gate)
            {
                if (activeCommand == null)
                {
                    return;
                }

                activeCommand.Output.Append(text);
                activeCommand.LastOutputAt = DateTime.UtcNow;
                lastActivityAt = DateTime.UtcNow;

                var current = activeCommand.Output.ToString();
                if (!current.Contains(promptMarker, StringComparison.Ordinal))
                {
                    return;
                }

                activeCommand.Output.Clear();
                activeCommand.Output.Append(current.Replace(promptMarker, string.Empty, StringComparison.Ordinal));
                activeCommand.Completed = true;
            }
        }

        async Task WatchShellTimeoutAsync()
        {
            while (!proc.HasExited)
            {
                var shouldTimeout = false;

                lock (gate)
                {
                    if (!shellClosed && DateTime.UtcNow - lastActivityAt >= idleTimeout)
                    {
                        shellClosed = true;
                        activeCommand = null;
                        shouldTimeout = true;
                    }
                }

                if (shouldTimeout)
                {
                    DialogManager.RemoveDialog(dialogKey);
                    TryCloseShellCore();
                    m.Reply("Shell因超时已退出。", false);
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }

        async Task ObserveCommandAsync(Message replyMessage, ShellCommandState commandState)
        {
            var startAt = DateTime.UtcNow;

            while (!proc.HasExited)
            {
                string? output = null;
                var completed = false;
                var shouldReply = false;

                lock (gate)
                {
                    if (!ReferenceEquals(activeCommand, commandState))
                    {
                        return;
                    }

                    var noOutputTooLong = commandState.Output.Length == 0 && DateTime.UtcNow - startAt > TimeSpan.FromSeconds(1);
                    var outputWentIdle = commandState.Output.Length != 0 && DateTime.UtcNow - commandState.LastOutputAt > TimeSpan.FromMilliseconds(600);

                    if (!commandState.Completed && !noOutputTooLong && !outputWentIdle)
                    {
                        goto ContinueWaiting;
                    }

                    output = commandState.Output.ToString();
                    completed = commandState.Completed;
                    shouldReply = true;

                    if (completed)
                    {
                        activeCommand = null;
                    }
                }

                if (shouldReply)
                {
                    ReplyShellOutput(replyMessage, output, completed);
                    return;
                }

                ContinueWaiting:
                await Task.Delay(100);
            }

            lock (gate)
            {
                if (!ReferenceEquals(activeCommand, commandState))
                {
                    return;
                }

                activeCommand = null;
            }

            ReplyShellOutput(replyMessage, commandState.Output.ToString(), completed: true);
        }

        void ReplyShellOutput(Message replyMessage, string? output, bool completed)
        {
            var text = string.IsNullOrWhiteSpace(output)
                ? completed
                    ? "（无输出）"
                    : "命令已发送，暂无输出；如果它正在等待输入，请继续发送内容。"
                : NormalizeOutput(output);

            _ = Task.Run(async () => await ReplyShellChunksAsync(replyMessage, text));
        }

        void TryCloseShell()
        {
            lock (gate)
            {
                if (shellClosed)
                {
                    return;
                }

                shellClosed = true;
                activeCommand = null;
            }

            TryCloseShellCore();
        }

        void TryCloseShellCore()
        {
            try
            {
                if (!proc.HasExited)
                {
                    proc.StandardInput.WriteLine("exit");
                    proc.StandardInput.Flush();
                }

                if (!proc.WaitForExit(1000) && !proc.HasExited)
                {
                    proc.Kill(true);
                }
            }
            catch
            {
                if (!proc.HasExited)
                {
                    proc.Kill(true);
                }
            }
            finally
            {
                proc.Dispose();
            }
        }

        static string NormalizeOutput(string output)
        {
            return output
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Trim();
        }

        static IEnumerable<string> SplitForReply(string text)
        {
            const int chunkSize = 1500;

            if (text.Length <= chunkSize)
            {
                yield return text;
                yield break;
            }

            for (var start = 0; start < text.Length; start += chunkSize)
            {
                var length = Math.Min(chunkSize, text.Length - start);
                yield return text.Substring(start, length);
            }
        }

        static async Task ReplyShellChunksAsync(Message replyMessage, string text)
        {
            const int chunkDelayMilliseconds = 200;

            foreach (var chunk in SplitForReply(text))
            {
                replyMessage.Reply(chunk, false);
                await Task.Delay(chunkDelayMilliseconds);
            }
        }
    }

    /// <summary>
    ///     restart bot
    /// </summary>
    /// <param name="m"></param>
    /// <returns></returns>
    [MarisaPluginNoDoc]
    [MarisaPluginCommand("reboot", "restart")]
    private static MarisaPluginTaskState Reboot(Message m)
    {
        if (!Commander.Contains(m.Sender.Id))
        {
            m.Reply("你没资格啊，你没资格。正因如此，你没资格。");
            return MarisaPluginTaskState.CompletedTask;
        }

        var startInfo = CreateRestartStartInfo();

        Process.Start(startInfo);
        m.Reply("正在重启");

        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            Environment.Exit(0);
        });

        return MarisaPluginTaskState.CompletedTask;

        static ProcessStartInfo CreateRestartStartInfo()
        {
            var processPath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;

            if (string.IsNullOrWhiteSpace(processPath))
            {
                throw new InvalidOperationException("Unable to determine current process path for reboot.");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName         = processPath,
                UseShellExecute  = false,
                WorkingDirectory = Environment.CurrentDirectory
            };

            foreach (var argument in Environment.GetCommandLineArgs().Skip(1))
            {
                startInfo.ArgumentList.Add(argument);
            }

            return startInfo;
        }
    }

    /// <summary>
    ///     reset bot cache
    /// </summary>
    [MarisaPluginNoDoc]
    [MarisaPluginCommand("reset")]
    private static MarisaPluginTaskState Reset(Message m, IEnumerable<MarisaPluginBase> plugins)
    {
        if (!Commander.Contains(m.Sender.Id))
        {
            m.Reply("你没资格啊，你没资格。正因如此，你没资格。");
            return MarisaPluginTaskState.CompletedTask;
        }

        var filtered =
            from plugin in plugins
            where plugin.GetType().IsSubclassOf(typeof(ICanReset))
            select plugin as ICanReset;

        foreach (var canReset in filtered)
        {
            canReset.Reset();
        }

        m.Reply("Done.");
        return MarisaPluginTaskState.CompletedTask;
    }

    private sealed class ShellCommandState
    {
        public StringBuilder Output { get; } = new();

        public DateTime LastOutputAt { get; set; } = DateTime.UtcNow;

        public bool Completed { get; set; }
    }
}
