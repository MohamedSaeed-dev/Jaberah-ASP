namespace Jaberah.Models.JaberahModels
{
    public class Version : BaseEntity
    {
        public required string LatestVersion { get; set; }
        public required string MinRequiredVersion { get; set; }
        public required string URL { get; set; }
    }
}
