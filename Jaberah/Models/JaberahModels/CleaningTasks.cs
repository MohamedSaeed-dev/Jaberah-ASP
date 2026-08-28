namespace Jaberah.Models.JaberahModels
{
    public class CleaningTask
    {
        public int Id { get; set; }
        public required string NameAr { get; set; }
        public string? NameEn { get; set; }
        public byte DisplayOrder { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<CleaningLog> Logs { get; set; } = [];
    }
}
