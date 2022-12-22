using Marisa.Backend.GoCq;
using Microsoft.Extensions.DependencyInjection;

namespace Marisa.StartUp;

public static class Program
{
    private static async Task Main(string[] args)
    {
        var provider = new Configuration(args).Config();

        var session = provider.GetService<GoCqBackend>()!;

        Console.WriteLine("---------------------------------------------------------------");
        Console.WriteLine("-- Running ");

        await session.Invoke();
    }
}