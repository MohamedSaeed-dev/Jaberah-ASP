namespace Jaberah.Models.ViewModels.FollowStudents
{
    public class GetFollowStudentForDay
    {
        public string StudentName { get; set; }
        public string SurahFromTeacher { get; set; }
        public string SurahToTeacher { get; set; }
        public int VerseFromTeacher { get; set; }
        public int VerseToTeacher { get; set; }
        public string RateTeacher { get; set; }
        public float PagesTeacher { get; set; }

        public string SurahFromFriend { get; set; }
        public string SurahToFriend { get; set; }
        public int VerseFromFriend { get; set; }
        public int VerseToFriend { get; set; }
        public string RateFriend { get; set; }
        public float PagesFriend { get; set; }


        public byte Attendance { get; set; }
        public byte Behavior { get; set; }
    }
}
