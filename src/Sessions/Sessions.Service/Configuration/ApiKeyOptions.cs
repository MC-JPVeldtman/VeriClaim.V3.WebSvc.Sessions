namespace Sessions.Service.Configuration
{
    public class ApiKeyOptions
    {
        public const string HeaderName = "X-Api-Key";

        /// <summary>One key per calling website, so a key can be rotated without touching the others.</summary>
        public string[] ApiKeys { get; set; } = [];
    }
}
