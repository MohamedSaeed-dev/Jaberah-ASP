namespace Jaberah.Models.JaberahModels
{
    public class Book : BaseEntity
    {
        public int GroupId { get; set; }
        public Group Group { get; set; }

        public string Title { get; set; }
        public string From { get; set; }
        public string To { get; set; }

        public DateTime Month { get; set; }
    }
}
