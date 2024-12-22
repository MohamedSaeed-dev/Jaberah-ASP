namespace Jaberah.Models.ViewModels.Reports
{
    class SemesterReportForView
    {
        public int StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public double GradeSum { get; set; }
        public int AttendanceSum { get; set; }
        public int BehaviorSum { get; set; }
        public double OralGradeSum { get; set; }
        public double PaperGradeSum { get; set; }
        public double MidFinalGrade { get; set; }
        public double Total { get; set; }
    }
}
