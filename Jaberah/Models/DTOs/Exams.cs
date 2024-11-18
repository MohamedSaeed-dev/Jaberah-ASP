namespace Jaberah.Models.DTOs
{
    public record UpsertMonthlyExamsDTO
    {
        public float? PaperExam { get; set; }
        public float? OralExam { get; set; }
    }
}
