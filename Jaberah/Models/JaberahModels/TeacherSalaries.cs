namespace Jaberah.Models.JaberahModels
{
    public class TeacherSalary : BaseEntity
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public float Salary { get; set; }
        public bool IsPaid { get; set; }
        public DateTime? PaidAt { get; set; }

        public required int GroupId { get; set; }
        public Group Group { get; set; }

        public required int TeacherId { get; set; }
        public Teacher Teacher { get; set; }
    }
}
