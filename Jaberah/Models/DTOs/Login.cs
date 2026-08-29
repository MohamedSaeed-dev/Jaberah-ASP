namespace Jaberah.Models.DTOs
{
    public class Login
    {
        public record LoginDTO
        {
            public string Username { get; set; }
            public string Password { get; set; }
            public string FCMToken { get; set; }
        }

        public record RefreshDTO
        {
            public string RefreshToken { get; set; }
        }

        // لا UserId هنا: هوية صاحب التوكن تُشتق من التوكن نفسه في AuthController.
        public record UpdateFCMTokenDTO
        {
            public string Token { get; set; }
        }
    }
}
