namespace PigeonPost.Bridge;

public enum SessionRegistrationResult
{
    Accepted,
    RejectedDuplicateHostIp,
    RejectedInvalidHandshake,
    RejectedServerShuttingDown
}
