using Jaberah.Models.JaberahModels;

namespace Jaberah.Models.DTOs
{
    public record AddGroupDTO
    {
        public string GroupName { get; set; }
        public Period Period { get; set; }
    }
    public record UpdateGroupDTO
    {
        public string? GroupName { get; set; }
        public Period? Period { get; set; }
    }
}
