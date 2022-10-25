using System;
using System.Diagnostics;
using SixLabors.ImageSharp;

namespace Marisa.Plugin.Test;

public static class Utils
{
    public static void Show(this Image image)
    {
        var name = Guid.NewGuid() + ".png";
        image.Save(name);
        OpenImage(name);
    }

    public static void OpenImage(string path)
    {
        var proc = new Process();
        proc.StartInfo = new ProcessStartInfo(path)
        {
            UseShellExecute = true
        };

        proc.Start();
    }
}