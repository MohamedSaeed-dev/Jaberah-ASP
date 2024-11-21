namespace Jaberah.Models.ViewModels.Reports
{
    class SemesterReportForView
    {
        public string StudentName { get; set; } = string.Empty;
        public double GradeSum { get; set; }
        public double AttendanceSum { get; set; }
        public double BehaviorSum { get; set; }
        public double OralGradeSum { get; set; }
        public double PaperGradeSum { get; set; }
        public double MidFinalGrade { get; set; }
        public double Total { get; set; }
    }

    class MonthlyReportForView
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
