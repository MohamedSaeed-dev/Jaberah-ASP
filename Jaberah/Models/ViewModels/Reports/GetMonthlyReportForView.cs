namespace Jaberah.Models.ViewModels.Reports
{
    class GetMonthlyReportForView
    {
        public int FollowStudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public SaveReviewData SaveData { get; set; }
        public SaveReviewData ReviewData { get; set; }
        public double SaveGrade { get; set; }
        public double ReviewGrade { get; set; }
        public int AttendanceGrade { get; set; }
        public int BehaviorGrade { get; set; }
        public double OralGrade { get; set; }
        public double PaperGrade { get; set; }
        public double Total { get; set; }
    }
    public class SaveReviewData
    {
        public FromToData From { get; set; }
        public FromToData To { get; set; }
        public double Pages { get; set; }
        public string Rate { get; set; } = string.Empty;
    }
    public class FromToData
    {
        public string SurahName { get; set; } = string.Empty;
        public int Verse { get; set; }
    }
}
