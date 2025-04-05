using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace OnlineChat;

public class ChatHub : Hub
{
    private static readonly ConcurrentDictionary<string, string> Users = new ();

    public async Task RegisterUser(string userName)
    {
        var connectionId = Context.ConnectionId;
        if (Users.ContainsKey(connectionId))
            Users.TryRemove(connectionId, out _);

        Users.TryAdd(connectionId, userName);
        
        await Clients.Others.SendAsync("UserConnected", userName);
        await Clients.Caller.SendAsync("ReceiveMessage", "System", $"Добро пожаловать, {userName}!");
        await UpdateUsersList();
    }

    
    public async Task Send(string message)
    {
        if (IsRegistered())
        {
            await Clients.All.SendAsync("Send", message);
        }
        else
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "system", "you aren;t registered");
        }
    }
    
    public async Task SendMessage(string user, string message)
    {
        if (IsRegistered())
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }
        else
        {
            await Clients.Caller.SendAsync("ReceiveMessage", "system", "you aren;t registered");
        }
    }
    
    // Этот метод вызывается автоматически при успешном подключении клиента
    public override async Task OnConnectedAsync()
    {
        // Получаем идентификатор подключения
        var connectionId = Context.ConnectionId;
        if (!Users.ContainsKey(connectionId))
        {
            //Users.TryAdd(connectionId, connectionId);
            //await UpdateUsersList();
        }
        
        // Получаем идентификатор пользователя (если используется аутентификация)
        var userId = Context.User?.Identity?.Name ?? "Anonymous";
        
        // Можно сохранить информацию о подключении в базе данных или памяти
        // Например, через dependency injection можно получить доступ к хранилищу
        
        // Уведомляем других клиентов о новом подключении
        // await Clients.Others.SendAsync("UserConnected", userId, connectionId);
        
        // Можно отправить новому клиенту информацию о текущем состоянии
        // Например, список уже подключенных пользователей
        // await Clients.Caller.SendAsync("ReceiveConnectedUsers", GetConnectedUsers());
        
        await base.OnConnectedAsync();
    }
    
    public override async Task OnDisconnectedAsync(Exception exception)
    {
        var connectionId = Context.ConnectionId;
        if (Users.TryRemove(connectionId, out var userName))
        {
            await Clients.Others.SendAsync("UserDisconnected", userName);
            await UpdateUsersList();
        }

        await base.OnDisconnectedAsync(exception);
    }
    
    private async Task UpdateUsersList()
    {
        var users = Users.Values.OrderBy(u => u).ToList();
        await Clients.All.SendAsync("UpdateUsersList", users);
    }

    private bool IsRegistered()
    {
        var connectionId = Context.ConnectionId;
        return Users.ContainsKey(connectionId);
    }
    
    // Пример метода для получения списка подключенных пользователей
    private List<string> GetConnectedUsers()
    {
        // Здесь должна быть логика получения списка подключенных пользователей
        // Например, из базы данных или хранилища в памяти
        return new List<string>();
    }
}