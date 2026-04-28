
using System.Text;
using Marisa.Backend.NapCat;
using Marisa.Plugin;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using NLog.Web;

namespace Marisa.StartUp;

public static class Program
{
    private static async Task Main(string[] args)
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

        // asp dotnet
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.ConfigLogger();
        foreach (var service in NapCatBackend.Config(Utils.Assembly().GetTypes()))
            builder.Services.Add(service);
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

        var webRootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
        if (Directory.Exists(webRootPath))
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(webRootPath),
                RequestPath  = ""
            });
        }

        app.MapGet("/", ctx =>
        {
            ctx.Response.Redirect("/index.html");
            return Task.CompletedTask;
        });
        if (Directory.Exists(webRootPath))
        {
            app.MapFallbackToFile("index.html");
        }

        // run
        await Task.WhenAll(app.RunAsync(), app.Services.GetService<BotDriver.BotDriver>()!.Invoke());
    }
}
