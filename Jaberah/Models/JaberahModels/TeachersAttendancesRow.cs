namespace Jaberah.Models.JaberahModels
{
    public class TeachersAttendancesRow
    {
        public int Id { get; set; }
        public int TeacherId { get; set; }
        public Teacher Teacher { get; set; }
        public int TeacherAttendanceId { get; set; }
        public TeachersAttendances TeachersAttendances { get; set; }
        public bool Signature { get; set; }
        public bool IsExcuse { get; set; }
    }
}
