using System.Diagnostics;
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
    private static MarisaPluginTaskState Shell(Message m)
    {
        if (!Commander.Contains(m.Sender.Id))
        {
            m.Reply("你没资格啊，你没资格。正因如此，你没资格。");
        }
        else
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo("cmd.exe")
                {
                    Arguments              = "/Q /K @echo off & cd c:\\",
                    UseShellExecute        = false,
                    RedirectStandardInput  = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true
                }
            };
            var output = "";

            proc.OutputDataReceived += (_, args) =>
            {
                if (args.Data == "CC_DONE")
                {
                    m.Reply(output.Trim(), false);
                }

                output += args.Data?.Trim() + "\n";
            };
            proc.ErrorDataReceived += (_, args) => { output += args.Data?.Trim() + "\n"; };
            proc.Start();
            proc.StandardInput.WriteLine("@echo off");

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            DialogManager.TryAddDialog((m.GroupInfo?.Id, m.Sender.Id), message =>
            {
                var command = message.Command;

                if (command.Span is "exit")
                {
                    proc.Close();
                    message.Reply("Shell退出了");
                    return Task.FromResult(MarisaPluginTaskState.CompletedTask);
                }

                output = "";
                proc.StandardInput.WriteLine(command);
                proc.StandardInput.WriteLine("echo CC_DONE");

                return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
            });

            m.Reply("Shell启动了");
        }

        return MarisaPluginTaskState.CompletedTask;
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

        var currentProcess = Process.GetCurrentProcess();

        var fileName  = currentProcess.MainModule!.FileName;
        var arguments = Environment.GetCommandLineArgs();

        var startInfo = new ProcessStartInfo
        {
            FileName        = fileName,
            Arguments       = string.Join(' ', arguments.Skip(1)),
            UseShellExecute = false
        };

        Process.Start(startInfo);

        Environment.Exit(0);

        return MarisaPluginTaskState.CompletedTask;
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
}
