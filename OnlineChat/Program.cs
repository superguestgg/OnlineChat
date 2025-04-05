using Microsoft.AspNetCore.SignalR;
using OnlineChat;

internal class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<RoomsService>();
        
        var app = builder.Build();

        var s = new Startup();
        s.Configure(app);
        app.Run();
    }
}
