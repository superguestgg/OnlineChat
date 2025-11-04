using Microsoft.AspNetCore.Mvc;

namespace OnlineChat.Authentication;

[Route("auth/google/callback")]
public class GoogleAuthCallbackController : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string credential)
    {
        try
        {
            var payload = await GoogleTokenVerifier.VerifyGoogleToken(credential);
            // Создайте сессию пользователя или выполните другие действия
            return Ok($"User authenticated: {payload.Email}");
        }
        catch (Exception ex)
        {
            return BadRequest($"Authentication failed: {ex.Message}");
        }
    }
}
