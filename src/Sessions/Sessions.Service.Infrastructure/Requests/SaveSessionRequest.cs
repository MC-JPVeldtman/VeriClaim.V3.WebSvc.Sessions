namespace Sessions.Service.Infrastructure.Requests
{
    public class SaveSessionRequest : SessionKeyRequest
    {
        public int LockId { get; set; }
        public byte[]? Data { get; set; }
        public int TimeoutMinutes { get; set; }

        /// <summary>True the first time a session is saved (ASP.NET's newItem).</summary>
        public bool IsNew { get; set; }
    }
}
