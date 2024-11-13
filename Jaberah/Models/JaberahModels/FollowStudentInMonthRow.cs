namespace Jaberah.Models.JaberahModels
{
    public class FollowStudentInMonthRow
    {
        public int Id { get; set; }
        public int Day { get; set; }
        public int WithTeacherId { get; set; }
        public int WithFriendId { get; set; }
        public WithTeacherFriend WithTeacher { get; set; }
        public WithTeacherFriend WithFriend { get; set; }
        public float Attendance { get; set; }
        public float Behavior { get; set; }
        public int FollowStudentInMonthId { get; set; }
        public FollowStudentInMonth FollowStudentInMonth { get; set; }
    }
    public class WithTeacherFriend
    {
        public int Id { get; set; }
        public int FromId { get; set; }
        public int ToId { get; set; }
        public Surah From { get; set; }
        public Surah To { get; set; }
        public float Pages { get; set; }
        public string Rate { get; set; } = string.Empty;
    }
    public class Surah
    {
        public int Id { get; set; }
        public string SurahName { get; set; } = string.Empty;
        public int Verse { get; set; }
    }
}
