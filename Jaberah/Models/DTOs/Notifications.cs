namespace Jaberah.Models.DTOs
{
    public record NotificationsDTO
    {
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
    }
}
