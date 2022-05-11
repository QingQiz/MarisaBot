using System.Diagnostics;

namespace Marisa.Plugin;

[MarisaPlugin(PluginPriority.Command)]
[MarisaPluginCommand(":cmd")]
public class Command : MarisaPluginBase
{
    private static IEnumerable<long> Commander => ConfigurationManager.Configuration.Commander;

    /// <summary>
    /// start an interactive shell
    /// </summary>
    /// <param name="m"></param>
    /// <returns></returns>
    [MarisaPluginCommand("shell")]
    private static MarisaPluginTaskState Shell(Message m)
    {
        if (!Commander.Contains(m.Sender!.Id))
        {
            m.Reply("你没资格啊，你没资格。正因如此，你没资格。");
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

            proc.OutputDataReceived += (_, args) =>
            {
                if (args.Data == "CC_DONE")
                {
                    m.Reply(output.Trim(), false);
                }
                output += args.Data?.Trim() + "\n";
            };
            proc.ErrorDataReceived+= (_, args) =>
            {
                output += args.Data?.Trim() + "\n";
            };
            proc.Start();
            proc.StandardInput.WriteLine("@echo off");

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();

            Dialog.AddHandler(m.GroupInfo?.Id, m.Sender.Id, message =>
            {
                var command = message.Command;

                if (command is "exit")
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
    /// restart bot
    /// </summary>
    /// <param name="m"></param>
    /// <returns></returns>
    [MarisaPluginCommand("reboot", "restart")]
    private static MarisaPluginTaskState Reboot(Message m)
    {
        if (!Commander.Contains(m.Sender!.Id))
        {
            m.Reply("你没资格啊，你没资格。正因如此，你没资格。");
        }
        else
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
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
        return MarisaPluginTaskState.CompletedTask;
    }
}