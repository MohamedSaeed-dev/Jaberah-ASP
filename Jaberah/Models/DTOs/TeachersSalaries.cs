namespace Jaberah.Models.DTOs
{
    public record UpsertTeachersSalariesDTO
    {
        public int TeacherId { get; set; }
        public float? Salary { get; set; }
        public bool? Signature { get; set; }
    }
}
