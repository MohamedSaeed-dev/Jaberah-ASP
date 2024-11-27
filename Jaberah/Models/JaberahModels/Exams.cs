namespace Jaberah.Models.JaberahModels
{
    public class Exam
    {
        public int Id { get; set; }
        public float PaperExam { get; set; }
        public float OralExam { get; set; }
        public int FollowStudentsId { get; set; }
        public FollowStudent FollowStudents { get; set; }
    }
}
