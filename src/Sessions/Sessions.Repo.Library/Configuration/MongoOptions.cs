namespace Sessions.Repo.Library.Configuration
{
    public class MongoOptions
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = "VeriClaimSessions";
        public string CollectionName { get; set; } = "Sessions";
    }
}
