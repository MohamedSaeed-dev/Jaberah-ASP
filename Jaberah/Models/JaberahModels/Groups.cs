namespace Jaberah.Models.JaberahModels
{
    public class Group
    {
        public int Id { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public Period Period { get; set; }
        public int? TeacherId { get; set; }
        public Teacher Teacher { get; set; }
        public ICollection<Student> Students { get; set; }
    }
    public enum Period
    {
        MORNING = 1,
        EVENING
    }
}
