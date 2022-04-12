using System.Diagnostics;
using QQBot.MiraiHttp.DI;
using QQBot.MiraiHttp.Entity;
using QQBot.MiraiHttp.Plugin;
using QQBot.Plugin.Shared;

namespace QQBot.Plugin;

[MiraiPlugin(PluginPriority.Command)]
[MiraiPluginCommand(":cmd")]
public class Command : MiraiPluginBase
{
    private static readonly long[] Commander =
    {
        642191352L,
    };

    /// <summary>
    /// start an interactive shell
    /// </summary>
    /// <param name="m"></param>
    /// <param name="ms"></param>
    /// <returns></returns>
    [MiraiPluginCommand("shell")]
    private static MiraiPluginTaskState Shell(Message m, MessageSenderProvider ms)
    {
        if (!Commander.Contains(m.Sender!.Id))
        {
            ms.Reply("你没资格啊，你没资格。正因如此，你没资格。", m);
        }
        else
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo("cmd.exe")
                {
                    Arguments = "/Q /K @echo off & cd c:\\",
                    UseShellExecute        = false,
                    RedirectStandardInput  = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                },
            };
            var output = "";

            proc.OutputDataReceived += (sender, args) =>
            {
                if (args.Data == "CC_DONE")
                {
                    ms.Reply(output.Trim(), m, false);
                }
                output += args.Data?.Trim() + "\n";
            };
            proc.ErrorDataReceived+= (sender, args) =>
            {
                output += args.Data?.Trim() + "\n";
            };
            proc.Start();
            proc.StandardInput.WriteLine("@echo off");

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            Dialog.AddHandler(m.GroupInfo?.Id, m.Sender.Id, (provider, message) =>
            {
                var command = message.Command;

                if (command is "exit")
                {
                    proc.Close();
                    ms.Reply("Shell退出了", m);
                    return Task.FromResult(MiraiPluginTaskState.CompletedTask);
                }

                output = "";
                proc.StandardInput.WriteLine(command);
                proc.StandardInput.WriteLine("echo CC_DONE");

                return Task.FromResult(MiraiPluginTaskState.ToBeContinued);
            });

            ms.Reply("Shell启动了", m);
        }

        return MiraiPluginTaskState.CompletedTask;
    }
 
    /// <summary>
    /// restart bot
    /// </summary>
    /// <param name="m"></param>
    /// <param name="ms"></param>
    /// <returns></returns>
    [MiraiPluginCommand("reboot")]
    private static MiraiPluginTaskState Reboot(Message m, MessageSenderProvider ms)
    {
        if (!Commander.Contains(m.Sender!.Id))
        {
            ms.Reply("你没资格啊，你没资格。正因如此，你没资格。", m);
        }
        else
        {
            var proc = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName        = Environment.GetCommandLineArgs()[0].Replace(".dll", ".exe"),
                    Arguments       = string.Join(' ', Environment.GetCommandLineArgs().Skip(1)),
                    UseShellExecute = true,
                    CreateNoWindow  = true,
                }
            };
            proc.Start();

            Environment.Exit(0);
        }
        return MiraiPluginTaskState.CompletedTask;
    }
}