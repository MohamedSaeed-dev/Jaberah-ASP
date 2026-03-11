namespace Jaberah.Models.JaberahModels
{
    public class Book : BaseEntity
    {
        public required int GroupId { get; set; }
        public required string Title { get; set; }
        public required string From { get; set; }
        public required string To { get; set; }
        public required DateTime Date { get; set; }

        public required Group Group { get; set; }
    }
}
