namespace Jaberah.Models.JaberahModels
{
    public class MidFinal : BaseEntity
    {
        public required int StudentId { get; set; }
        public required DateTime FromDate { get; set; }
        public required DateTime ToDate { get; set; }
        public required float Grade { get; set; }

        public Student Student { get; set; } = default!;
    }
}
