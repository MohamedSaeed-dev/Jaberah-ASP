namespace Jaberah.Models.JaberahModels
{
    public class TeachersSalaries : BaseEntity
    {
        public DateTime Date { get; set; }
        public ICollection<TeachersSalariesRow> TeachersSalariesRows { get; set; }
    }
}
