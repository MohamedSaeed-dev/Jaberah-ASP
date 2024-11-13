namespace Jaberah.Models.JaberahModels
{
    public class Exam
    {
        public int Id { get; set; }
        public float PaperExam { get; set; }
        public float OralExam { get; set; }
        public int FollowStudentInMonthId { get; set; }
        public FollowStudentInMonth FollowStudentInMonth { get; set; }
    }
}
