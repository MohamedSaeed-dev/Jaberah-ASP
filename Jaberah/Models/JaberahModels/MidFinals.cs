namespace Jaberah.Models.JaberahModels
{
    public class MidFinal
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public float Grade { get; set; }

        public Student Student { get; set; }
    }
}
