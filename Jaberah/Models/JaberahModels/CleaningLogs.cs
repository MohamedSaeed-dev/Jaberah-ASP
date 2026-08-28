namespace Jaberah.Models.JaberahModels
{
    public class CleaningLog : BaseEntity
    {
        public int StudentId { get; set; }
        public Student Student { get; set; } = default!;

        public int CleaningTaskId { get; set; }
        public CleaningTask CleaningTask { get; set; } = default!;

        public DateOnly Date { get; set; }

        public bool IsCompleted { get; set; }
        public string? Notes { get; set; }
    }
}
