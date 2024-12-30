namespace Jaberah.Models.JaberahModels
{
    public class FollowStudentRow
    {
        public int Id { get; set; }
        public int Day { get; set; }
        public int WithTeacherId { get; set; }
        public int WithFriendId { get; set; }
        public WithTeacherFriend WithTeacher { get; set; }
        public WithTeacherFriend WithFriend { get; set; }
        public byte Attendance { get; set; }
        public byte Behavior { get; set; }
        public string? Notes { get; set; }
        public int FollowStudentsId { get; set; }
        public FollowStudent FollowStudents { get; set; }
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
