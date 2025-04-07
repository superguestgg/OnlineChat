using Microsoft.AspNetCore.SignalR;
using OnlineChat;

internal static class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.ConfigureServices();
        
        var app = builder.Build();

        var s = new Startup();
        s.Configure(app);
        app.Run();
    }

    private static void ConfigureServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSignalR();
        serviceCollection.AddSingleton<RoomsService>();
        serviceCollection.AddSingleton<PrivateRoomsService>();
    }
}
