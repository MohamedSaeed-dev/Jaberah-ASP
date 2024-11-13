namespace Jaberah.Models.JaberahModels
{
    public class TeachersAttendances
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public ICollection<TeachersAttendancesRow> TeachersAttendancesRows { get; set; }
    }
}
