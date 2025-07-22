using System;
using System.Net.Http;
using System.Threading.Tasks;


namespace OnlineChat.Authentication;


public class GoogleAuthService
{
    private static readonly HttpClient client = new HttpClient();

    public static async Task<string> GetAccessToken(string authCode)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token");

        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = authCode,
            ["client_id"] = "YOUR_CLIENT_ID",
            ["client_secret"] = "YOUR_CLIENT_SECRET",
            ["redirect_uri"] = "https://onlinechat.na4u.ru/auth/google/callback",
            ["grant_type"] = "authorization_code"
        });

        var response = await client.SendAsync(request);
        var responseString = await response.Content.ReadAsStringAsync();

        // Обработка ответа и извлечение access_token
        // Здесь вы можете использовать библиотеку для работы с JSON, например Newtonsoft.Json или System.Text.Json
        return responseString;
    }
}
