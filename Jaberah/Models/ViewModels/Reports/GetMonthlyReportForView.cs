namespace Jaberah.Models.ViewModels.Reports
{
    class GetMonthlyReportForView
    {
        public string StudentName { get; set; } = string.Empty;
        public SaveReviewData SaveData { get; set; }
        public SaveReviewData ReviewData { get; set; }
        public int AttendanceGrade { get; set; }
        public int BehaviorGrade { get; set; }
        public float OralGrade { get; set; }
        public float PaperGrade { get; set; }
        public float Total { get; set; }
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
