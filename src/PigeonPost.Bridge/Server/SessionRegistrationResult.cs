namespace PigeonPost.Bridge;

public enum SessionRegistrationResult
{
    Accepted,
    RejectedDuplicateId,
    RejectedDuplicateHostIp,
    RejectedInvalidHandshake,
    RejectedServerShuttingDown
}
