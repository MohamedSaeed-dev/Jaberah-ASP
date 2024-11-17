namespace Jaberah.Models.DTOs
{
    public record UpsertTeachersAttendancesDTO
    {
        public int TeacherId { get; set; }
        public TeachersAttendancesModel Data { get; set; }
    }
    public class TeachersAttendancesModel
    {
        public bool? IsExcuse { get; set; }
        public bool? Signature { get; set; }
    }
}
