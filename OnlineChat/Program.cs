using Microsoft.AspNetCore.SignalR;
using OnlineChat.Services;

namespace OnlineChat;

public static class Program
{
    public static void Main(string[] args)
    {
        Console.WriteLine(string.Join(" ", args));
        Console.WriteLine(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));
        Console.WriteLine("Environment.GetEnvironmentVariable(\"ASPNETCORE_ENVIRONMENT\")");
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        builder.Logging.AddFilter("Microsoft.AspNetCore.SignalR", LogLevel.Debug);
        builder.Logging.AddFilter("Microsoft.AspNetCore.Http.Connections", LogLevel.Debug);
        builder.Services.ConfigureServices();
        
        var app = builder.Build();

        var s = new Startup();
        s.Configure(app);
        app.Run();
    }

    private static void ConfigureServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSignalR(options =>
        {
            options.MaximumReceiveMessageSize = 100 * 1024 * 1024; // 100MB
            options.EnableDetailedErrors = true;
            options.StreamBufferCapacity = 10; // Размер буфера для стриминга
        });
        serviceCollection.AddSingleton<RoomsService>();
        serviceCollection.AddSingleton<PrivateRoomsService>();
    }
}