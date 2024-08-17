using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Marisa.StartUp.Controllers;

[ApiController]
[Route("Api/[controller]/[action]")]
public class WebContext : Controller
{
    public WebContext()
    {
        var path = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "WebContextHistory");
        // create if not exists
        Directory.CreateDirectory(path);
    }

    private static void WriteHistory(Guid id, string name, string str)
    {
        var path = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "WebContextHistory");

        Directory.CreateDirectory(path);

        var file = Path.Join(path, $"{name}.{id}");
        System.IO.File.WriteAllText(file, str);
    }

    private static bool TryReadHistory(Guid id, string name, out string output)
    {
        var path = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "WebContextHistory");

        if (id == Guid.Empty)
        {
            foreach (var i in Directory.GetFiles(path))
            {
                var file = Path.GetFileName(i);
                if (file.StartsWith(name))
                {
                    output = System.IO.File.ReadAllText(i);
                    return true;
                }
            }
        }
        else
        {
            foreach (var i in Directory.GetFiles(path))
            {
                var file = Path.GetFileName(i);
                if (file.StartsWith(name) && file.EndsWith(id.ToString()))
                {
                    output = System.IO.File.ReadAllText(i);
                    return true;
                }
            }
        }

        output = "";
        return false;
    }

    [HttpGet]
    public string Get(Guid id, string name)
    {
        if (TryReadHistory(id, name, out var output))
        {
            return output;
        }

        var obj = Backend.Shared.WebContext.Get(id, name);
        var str = obj is string ? obj.ToString()! : JsonConvert.SerializeObject(obj);

        Task.Run(() => WriteHistory(id, name, str));

        return str;
    }
}