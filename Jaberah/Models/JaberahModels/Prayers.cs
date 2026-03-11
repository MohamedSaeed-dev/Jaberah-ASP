namespace Jaberah.Models.JaberahModels
{
    public class Prayer
    {
        public int Id { get; set; }
        public required string NameAr { get; set; }
        public required string NameEn { get; set; }
        public byte DefaultRakats { get; set; }
        public byte DisplayOrder { get; set; }

        public ICollection<StudentPrayerAttendance> Attendances { get; set; } = [];
    }
}
