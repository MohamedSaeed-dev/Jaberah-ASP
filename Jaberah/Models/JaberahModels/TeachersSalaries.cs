namespace Jaberah.Models.JaberahModels
{
    public class TeachersSalaries
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public ICollection<TeachersSalariesRow> TeachersSalariesRows { get; set; }
    }
}
