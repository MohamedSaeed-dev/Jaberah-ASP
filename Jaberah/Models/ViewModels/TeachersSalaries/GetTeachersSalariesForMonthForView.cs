namespace Jaberah.Models.ViewModels.TeachersSalaries
{
    public class GetTeachersSalariesForMonthForView
    {
        public string TeacherName { get; set; }
        public float Salary { get; set; }
        public float NetSalary { get; set; }
        public bool Signature { get; set; }
        public int DaysAbsence { get; set; }
    }
}
