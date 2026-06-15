using NUnit.Framework;

namespace PigeonPost.Bridge.Tests.Pontifex;

[TestFixture]
public class MultiClientDirectTransportTests
{
    [Test]
    [Ignore("Multi-client transport integration pending server hub + protocol implementation (action-04/05/06)")]
    public void Server_Accepts_TwoClients_Simultaneously()
    {
        Assert.Inconclusive("Stage for multi-client integration testing");
    }

    [Test]
    [Ignore("Multi-client transport integration pending server hub + protocol implementation (action-04/05/06)")]
    public void DuplicateClientId_IsReported_ToClient()
    {
        Assert.Inconclusive("Stage for duplicate client ID rejection testing");
    }

    [Test]
    [Ignore("Multi-client transport integration pending server hub + protocol implementation (action-04/05/06)")]
    public void Traffic_ToAdvertisedHost_ReachesCorrectClient()
    {
        Assert.Inconclusive("Stage for host-route delivery testing");
    }

    [Test]
    [Ignore("Multi-client transport integration pending server hub + protocol implementation (action-04/05/06)")]
    public void OneClientDisconnect_DoesNotAffect_Others()
    {
        Assert.Inconclusive("Stage for sibling-client disconnect testing");
    }
}
