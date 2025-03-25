namespace Jaberah.Models.JaberahModels
{
    public class TeachersAttendances : BaseEntity
    {
        public DateTime Date { get; set; }
        public ICollection<TeachersAttendancesRow> TeachersAttendancesRows { get; set; }
    }
}
