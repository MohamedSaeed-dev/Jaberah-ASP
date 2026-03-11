namespace Jaberah.Models.DTOs
{
    public record UpsertMonthlyExamsDTO
    {
        public float? PaperExam { get; set; }
        public float? OralExam { get; set; }

        public DateTime Date { get; set; }
        public required int StudentId { get; set; }
    }
}
