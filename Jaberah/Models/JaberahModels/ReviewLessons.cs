namespace Jaberah.Models.JaberahModels
{
    public class ReviewLesson : BaseEntity
    {
        public required DateTime Date { get; set; }
        public required string SurahFrom { get; set; }
        public required string SurahTo { get; set; }
        public required int VerseFrom { get; set; }
        public required int VerseTo { get; set; }
        public required string Rate { get; set; }
        public required float Pages { get; set; }
        public string? Notes { get; set; }

        public required int StudentId { get; set; }
        public Student Student { get; set; }
    }
}
