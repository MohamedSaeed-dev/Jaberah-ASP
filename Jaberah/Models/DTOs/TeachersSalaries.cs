namespace Jaberah.Models.DTOs
{
    public record UpsertTeachersSalariesDTO
    {
        public int TeacherId { get; set; }
        public int GroupId { get; set; }
        public float? Salary { get; set; }
        public bool? IsPaid { get; set; }
    }
}
