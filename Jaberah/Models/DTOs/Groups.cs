using Jaberah.Models.JaberahModels;

namespace Jaberah.Models.DTOs
{
    public record AddGroupDTO
    {
        public string GroupName { get; set; }
        public int? TeacherId { get; set; }
        public Period Period { get; set; }
    }
    public record UpdateGroupDTO
    {
        public string? GroupName { get; set; }
        public int? TeacherId { get; set; }
        public Period? Period { get; set; }
        public TimeOnly? WindowStart { get; set; }
        public TimeOnly? WindowEnd { get; set; }
        public decimal? FlexibleMinutes { get; set; }
    }
}
