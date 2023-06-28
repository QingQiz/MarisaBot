using Marisa.Backend.GoCq;
using Marisa.Backend.Mirai;
using Marisa.BotDriver.DI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using NuGet.Packaging;

namespace Marisa.StartUp;

public static class Program
{
    private static async Task Main(string[] args)
    {
        var useMirai = !(args.Length > 3 && args[3] == "gocq");

        // asp dotnet
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddRange(useMirai ? MiraiBackend.Config(Plugin.Utils.Assembly()) : GoCqBackend.Config(Plugin.Utils.Assembly()));
        builder.Services.ConfigLogger();
        builder.WebHost.UseUrls("http://localhost:14311");

        var app = builder.Build();
        app.UseSwagger();
        app.UseSwaggerUI();
        app.MapControllers();
        app.UseDeveloperExceptionPage();

        app.UseCors(c => c.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(Path.Combine(builder.Environment.ContentRootPath, "wwwroot")),
            RequestPath  = ""
        });

        app.Services.GetService<DictionaryProvider>()!
            .Add("QQ", long.Parse(args[1]))
            .Add("ServerAddress", args[0])
            .Add("AuthKey", args[2]);

        app.MapGet("/", ctx =>
        {
            ctx.Response.Redirect("/index.html");
            return Task.CompletedTask;
        });
        app.MapFallbackToFile("index.html");

        // run
        if (!useMirai)
        {
            await Task.WhenAll(app.RunAsync(), app.Services.GetService<GoCqBackend>()!.Invoke());
        }
        else
        {
            await Task.WhenAll(app.RunAsync(), app.Services.GetService<MiraiBackend>()!.Invoke());
        }
        // await app.RunAsync();
    }
}