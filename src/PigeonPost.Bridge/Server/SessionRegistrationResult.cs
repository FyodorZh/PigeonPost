namespace PigeonPost.Bridge.Server;

public enum SessionRegistrationResult
{
    Accepted,
    RejectedDuplicateId,
    RejectedDuplicateHostIp,
    RejectedInvalidHandshake,
    RejectedServerShuttingDown
}
