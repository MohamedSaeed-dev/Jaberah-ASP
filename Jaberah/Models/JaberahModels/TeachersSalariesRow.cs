namespace Jaberah.Models.JaberahModels
{
    public class TeachersSalariesRow
    {
        public int Id { get; set; }
        public int TeacherId { get; set; }
        public Teacher Teacher { get; set; }
        public int? DaysAbsence { get; set; }
        public float Salary { get; set; }
        public float NetSalary { get; set; }
        public bool Signature { get; set; }
        public int TeachersSalariesId { get; set; }
        public TeachersSalaries TeachersSalaries { get; set; }
    }
}
