namespace Probate.Api.Infrastructure.Options
{
    public class CDogsOptions
    {
        public const string SectionName = "CDogs";
        public string BaseUrl { get; set; } = string.Empty;
        public string TokenUrl { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
    }
}
