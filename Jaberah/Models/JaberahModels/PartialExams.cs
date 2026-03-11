namespace Jaberah.Models.JaberahModels
{
    public class PartialExam : BaseEntity
    {
        public required int StudentId { get; set; }
        public required DateOnly Date { get; set; }

        public decimal Question1 { get; set; }
        public decimal Question2 { get; set; }
        public decimal Question3 { get; set; }
        public decimal Question4 { get; set; }
        public decimal Question5 { get; set; }
        public decimal Question6 { get; set; }
        public decimal Question7 { get; set; }
        public decimal Question8 { get; set; }
        public decimal Question9 { get; set; }
        public decimal Question10 { get; set; }

        public decimal Performance { get; set; }
        public string? Tester { get; set; }
        public string? Rate { get; set; }
        public string? Part { get; set; }
        public string? Notes { get; set; }
        public decimal TotalScore { get; set; }

        public decimal Score => Question1 + Question2 + Question3 + Question4 + Question5 + Question6 + Question7 + Question8 + Question9 + Question10;

        public Student Student { get; set; } = default!;
    }
}
