namespace Jaberah.Models.DTOs
{
    public record UpsertTeachersAttendancesDTO
    {
        public int TeacherId { get; set; }
        public bool? IsExcuse { get; set; }
        public bool? Signature { get; set; }
    }
}
