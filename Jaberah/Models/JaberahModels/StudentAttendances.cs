namespace Jaberah.Models.JaberahModels
{
    public class StudentAttendance : BaseEntity
    {
        public DateTime Date { get; set; }
        public float Attendance { get; set; }
        public float Behavior { get; set; }
        public required int StudentId { get; set; }
        public Student Student { get; set; }
    }
}
