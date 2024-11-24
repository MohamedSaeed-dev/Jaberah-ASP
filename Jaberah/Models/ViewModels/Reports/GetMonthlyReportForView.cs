namespace Jaberah.Models.ViewModels.Reports
{
    class GetMonthlyReportForView
    {
        public string StudentName { get; set; } = string.Empty;
        public SaveReviewData SaveData { get; set; }
        public SaveReviewData ReviewData { get; set; }
        public double AttendanceGrade { get; set; }
        public double BehaviorGrade { get; set; }
        public double OralGrade { get; set; }
        public double PaperGrade { get; set; }
        public double Total { get; set; }
    }
    class SaveReviewData
    {
        public FromToData From { get; set; }
        public FromToData To { get; set; }
        public float Pages { get; set; }
        public string Rate { get; set; } = string.Empty;
    }
    class FromToData
    {
        public string SurahName { get; set; } = string.Empty;
        public int Verse { get; set; }
    }
}
