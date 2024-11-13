namespace Jaberah.Models.JaberahModels
{
    public class FollowStudentInMonth
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public int StudentId { get; set; }
        public Student Student { get; set; }
        public ICollection<FollowStudentInMonthRow> FollowStudentInMonthRows { get; set; }
        public Exam Exams { get; set; }
    }
}
