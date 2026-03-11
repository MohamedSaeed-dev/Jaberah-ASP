namespace Jaberah.Models.JaberahModels
{
    public class Exam : BaseEntity
    {
        public required int StudentId { get; set; }
        public float PaperExam { get; set; }
        public float OralExam { get; set; }

        public DateTime Date { get; set; }

        public required Student Student { get; set; }
    }
}
