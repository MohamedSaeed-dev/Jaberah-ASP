namespace Jaberah.Models.DTOs
{
    public record UpsertBookDTO
    {
        public string? Title { get; set; }
        public string? From { get; set; }
        public string? To { get; set; }
        public DateOnly Date { get; set; }
    }
}
