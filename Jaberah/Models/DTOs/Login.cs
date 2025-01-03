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
    }
}
