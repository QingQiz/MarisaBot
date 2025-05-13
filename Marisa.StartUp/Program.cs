
using Marisa.Backend.Lagrange;
using Marisa.BotDriver.DI;
using Marisa.Plugin;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using NLog.Web;
using osu.Game.Extensions;

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
        builder.Services.ConfigLogger();
        builder.Services.AddRange(LagrangeBackend.Config(Utils.Assembly().GetTypes()));
        builder.WebHost.UseUrls("http://0.0.0.0:14311");

        // use nLog for logging
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
        builder.Host.UseNLog();
        builder.Services.AddExceptionHandler<ExceptionHandler>();

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
        await Task.WhenAll(app.RunAsync(), app.Services.GetService<BotDriver.BotDriver>()!.Invoke());
    }
}