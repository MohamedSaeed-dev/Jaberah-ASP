namespace Jaberah.Models.JaberahModels
{
    public class FollowStudent
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public int StudentId { get; set; }
        public Student Student { get; set; }
        public ICollection<FollowStudentRow> FollowStudentsRows { get; set; }
        public Exam Exams { get; set; }
    }
}
