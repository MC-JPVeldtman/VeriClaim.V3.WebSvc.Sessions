using System.Runtime.Serialization;

namespace VeriClaim.SessionState.Client
{
    // Wire contracts for the Sessions API. Byte arrays travel as base64 strings to match System.Text.Json.

    [DataContract]
    internal sealed class AcquireSessionBody
    {
        [DataMember(Name = "exclusive")] public bool Exclusive { get; set; }
        [DataMember(Name = "browserKeyHash")] public string BrowserKeyHash { get; set; }
    }

    [DataContract]
    internal sealed class AcquireSessionResult
    {
        public const string StatusFound = "Found";
        public const string StatusLocked = "Locked";

        [DataMember(Name = "status")] public string Status { get; set; }
        [DataMember(Name = "lockId")] public int LockId { get; set; }
        [DataMember(Name = "lockAgeSeconds")] public int LockAgeSeconds { get; set; }
        [DataMember(Name = "timeoutMinutes")] public int TimeoutMinutes { get; set; }
        [DataMember(Name = "data")] public string Data { get; set; }
        [DataMember(Name = "isUninitialized")] public bool IsUninitialized { get; set; }
    }

    [DataContract]
    internal sealed class SaveSessionBody
    {
        [DataMember(Name = "lockId")] public int LockId { get; set; }
        [DataMember(Name = "data")] public string Data { get; set; }
        [DataMember(Name = "timeoutMinutes")] public int TimeoutMinutes { get; set; }
        [DataMember(Name = "isNew")] public bool IsNew { get; set; }
        [DataMember(Name = "browserKeyHash")] public string BrowserKeyHash { get; set; }
    }

    [DataContract]
    internal sealed class SaveSessionResult
    {
        [DataMember(Name = "saved")] public bool Saved { get; set; }
    }

    [DataContract]
    internal sealed class CreateUninitializedSessionBody
    {
        [DataMember(Name = "timeoutMinutes")] public int TimeoutMinutes { get; set; }
        [DataMember(Name = "browserKeyHash")] public string BrowserKeyHash { get; set; }
    }

    [DataContract]
    internal sealed class ReleaseSessionBody
    {
        [DataMember(Name = "lockId")] public int LockId { get; set; }
    }

    [DataContract]
    internal sealed class TouchSessionBody
    {
        [DataMember(Name = "browserKeyHash")] public string BrowserKeyHash { get; set; }
    }
}
