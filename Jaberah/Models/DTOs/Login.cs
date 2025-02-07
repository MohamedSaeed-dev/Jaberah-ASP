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

        public record UpdateFCMTokenDTO
        {
            public int UserId { get; set; }
            public string Token { get; set; }
        }
    }
}
