namespace Jaberah.Models.JaberahModels
{
    public class Version
    {
        public int Id { get; set; }
        public required string LatestVersion { get; set; }
        public required string MinRequiredVersion { get; set; }
        public required string URL { get; set; }
    }
}
