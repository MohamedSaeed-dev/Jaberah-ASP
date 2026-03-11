namespace Jaberah.Models.JaberahModels
{
    public class Notification : BaseEntity
    {
        public required string Title { get; set; }
        public string? Body { get; set; } = string.Empty;
    }
}
