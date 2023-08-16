using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Marisa.StartUp;

[ApiController]
[Route("Api/[controller]/[action]")]
public class WebContext : Controller
{
    private static void WriteHistory(Guid id, string name, object obj)
    {
        var path = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "WebContextHistory");

        Directory.CreateDirectory(path);

        var file = Path.Join(path, $"{name}.{id}");
        System.IO.File.WriteAllText(file, obj is string ? obj.ToString() : JsonConvert.SerializeObject(obj));
    }

    private static string ReadHistory(string name)
    {
        var path = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "WebContextHistory");

        foreach (var i in Directory.GetFiles(path))
        {
            var file = Path.GetFileName(i);
            if (file.StartsWith(name))
            {
                return System.IO.File.ReadAllText(i);
            }
        }
        throw new Exception($"No history found for {name}");
    }

    [HttpGet]
    public object Get(Guid id, string name)
    {
        if (id == Guid.Empty)
        {
            return ReadHistory(name);
        }

        var obj = Utils.WebContext.Get(id, name);

        Task.Run(() => WriteHistory(id, name, obj));

        return obj;
    }
}
