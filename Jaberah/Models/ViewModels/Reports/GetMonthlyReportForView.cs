namespace Jaberah.Models.ViewModels.Reports
{
    class GetMonthlyReportForView
    {
        public List<BooksData> Books { get; set; }
        public List<GetMonthlyReportData> Data { get; set; }

    }
    public class GetMonthlyReportData
    {
        public int FollowStudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public SaveReviewData SaveData { get; set; }
        public SaveReviewData ReviewData { get; set; }
        public double SaveGrade { get; set; }
        public double ReviewGrade { get; set; }
        public double AttendanceGrade { get; set; }
        public double BehaviorGrade { get; set; }
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

    public class BooksData
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public DateTime Date { get; set; }
    }
}
