namespace Sessions.Service.Infrastructure.Responses
{
    public class SaveSessionResponse
    {
        /// <summary>False when the caller no longer held the lock, or the id belongs to another browser.</summary>
        public bool Saved { get; set; }
    }
}
