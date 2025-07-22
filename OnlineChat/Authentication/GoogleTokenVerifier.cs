using Google.Apis.Auth;

namespace OnlineChat.Authentication;


public class GoogleTokenVerifier
{
    public static async Task<GoogleJsonWebSignature.Payload> VerifyGoogleToken(string token)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings()
            {
                Audience = new[] { "138534674847-bs48lll21krh9dcfe5ve2bkhbtl5a32k.apps.googleusercontent.com" } // Замените на ваш Client ID
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(token, settings);

            if (payload == null)
            {
                throw new Exception("Invalid token");
            }
            return payload;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Token verification failed: {ex.Message}");
            throw;
        }
    }
}
