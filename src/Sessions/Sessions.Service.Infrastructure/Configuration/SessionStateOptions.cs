namespace Sessions.Service.Infrastructure.Configuration
{
    public class SessionStateOptions
    {
        public int MinTimeoutMinutes { get; set; } = 1;
        public int MaxTimeoutMinutes { get; set; } = 1440;

        /// <summary>Mongo documents are capped at 16MB, so leave headroom for the other fields.</summary>
        public int MaxDataBytes { get; set; } = 15 * 1024 * 1024;
    }
}
