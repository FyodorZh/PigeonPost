# Stage 9: Integration & End-to-End Tests

## Goal

Verify the complete system works with real TUN devices on Linux. This stage requires
a Linux machine (or VM) with root/sudo access for creating TUN devices.

## Prerequisites

- All previous stages complete.
- Linux machine with `ip tuntap` available.
- Root or `sudo` access to create TUN devices.
- Two TUN devices created and routing configured for the debug-mode test.

## Test Setup

### Creating TUN Devices

```bash
sudo ip tuntap add dev tunA mode tun
sudo ip tuntap add dev tunB mode tun
sudo ip link set tunA up
sudo ip link set tunB up
```

### Configuring Routing (Debug Mode Test)

For the debug mode test where both TUNs are on the same machine:

```bash
# Assign IPs
sudo ip addr add 10.99.0.1/30 dev tunA
sudo ip addr add 10.99.0.2/30 dev tunB

# Enable forwarding
sudo sysctl -w net.ipv4.ip_forward=1

# No routing needed for direct TUN-to-TUN in the same subnet — but we need
# the kernel to send packets through these interfaces. Use:
sudo ip route add 10.99.0.2/32 dev tunA
sudo ip route add 10.99.0.1/32 dev tunB
```

### Cleanup After Tests

```bash
sudo ip link delete tunA
sudo ip link delete tunB
```

## Test Cases

### Test 1: TUN Device Open/Close

```csharp
[TestFixture]
[Category("Integration")]
public class TunDeviceIntegrationTests
{
    [Test]
    public void Open_PreConfiguredTunDevice_Succeeds()
    {
        using var device = new TunDevice();
        Assert.That(() => device.Open("tunA"), Throws.Nothing);
        Assert.That(device.IsOpen, Is.True);
        Assert.That(device.Name, Is.EqualTo("tunA"));
    }

    [Test]
    public void Open_NonexistentTun_FailsWithIOException()
    {
        using var device = new TunDevice();
        Assert.That(() => device.Open("nonexistent_tun_xyz"), Throws.InstanceOf<IOException>());
    }

    [Test]
    public void Open_ThenClose_ThenOpenAgain_Succeeds()
    {
        using var device = new TunDevice();
        device.Open("tunA");
        device.Close();
        Assert.That(device.IsOpen, Is.False);
        device.Open("tunA");
        Assert.That(device.IsOpen, Is.True);
    }

    [Test]
    public void WriteToTun_CanReadFromPeerTun()
    {
        // Configure: tunA (10.99.0.1) ←bridge→ tunB (10.99.0.2)
        // Send a raw IP packet from tunA addressed to tunB's IP.
        // Read on tunB and verify.

        using var tunA = new TunDevice();
        using var tunB = new TunDevice();
        tunA.Open("tunA");
        tunB.Open("tunB");

        // Craft a minimal IPv4 ICMP packet from 10.99.0.1 → 10.99.0.2:
        byte[] packet = BuildIcmpPacket(
            srcIp: "10.99.0.1",
            dstIp: "10.99.0.2",
            payload: new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

        tunA.Write(packet);

        // Read on tunB (with timeout)
        byte[] readBuf = new byte[65536];
        int bytesRead = ReadWithTimeout(tunB, readBuf, TimeSpan.FromSeconds(2));
        Assert.That(bytesRead, Is.GreaterThan(0));

        // Verify it's the same packet (or at least an IP packet for our destination)
        Assert.That(readBuf[0] >> 4, Is.EqualTo(4)); // IPv4 version
    }

    private static int ReadWithTimeout(TunDevice device, byte[] buffer, TimeSpan timeout)
    {
        // Blocking read with timeout — use a separate thread
        int result = -1;
        var thread = new Thread(() =>
        {
            try { result = device.Read(buffer); }
            catch (IOException) { result = -1; }
        });
        thread.Start();
        if (!thread.Join(timeout))
        {
            // Timeout — thread is still blocked on read. We can't easily cancel.
            // For testing, just return -1.
            return -1;
        }
        return result;
    }
}
```

### Test 2: Debug Mode Bridge (Real TUNs + Direct Transport)

This is the main E2E test. It:
1. Opens two real TUN devices.
2. Creates two bridges with Direct transport.
3. Injects an IP packet into tunA.
4. Verifies it arrives at tunB.
5. Injects an IP packet into tunB.
6. Verifies it arrives at tunA.

```csharp
[TestFixture]
[Category("Integration")]
public class DebugModeEndToEndTests
{
    private const string TunAName = "tunA";
    private const string TunBName = "tunB";
    private const string DirectServerName = "e2e_test_server";

    private AckRawDirectServer _server = null!;
    private AckRawDirectClient _client = null!;
    private TunDevice _tunA = null!;
    private TunDevice _tunB = null!;
    private Bridge _bridgeA = null!;
    private Bridge _bridgeB = null!;

    [SetUp]
    public void SetUp()
    {
        StaticLogger.Instance.AddConsumer(new ConsoleConsumer());

        _tunA = new TunDevice();
        _tunB = new TunDevice();
        _tunA.Open(TunAName);
        _tunB.Open(TunBName);

        var bufferA = new PacketBuffer(100_000);
        var bufferB = new PacketBuffer(100_000);

        _bridgeA = new Bridge(_tunA, bufferA, StaticLogger.Instance, verbose: true);
        _bridgeB = new Bridge(_tunB, bufferB, StaticLogger.Instance, verbose: true);

        // Create Direct transport
        _server = new AckRawDirectServer(DirectServerName, StaticLogger.Instance, MemoryRental.Shared);
        _server.Init(new BridgeServerAcknowledger(_bridgeA));

        _client = new AckRawDirectClient(DirectServerName, StaticLogger.Instance, MemoryRental.Shared);
        _client.Init(new BridgeClientHandler(_bridgeB));

        // Start bridges
        _bridgeA.Start();
        _bridgeB.Start();

        // Start transport
        _server.Start(_ => { });
        _client.Start(_ => { });

        Thread.Sleep(200); // wait for handshake
    }

    [TearDown]
    public void TearDown()
    {
        _client.Stop(StopReason.UserIntention);
        _bridgeB.Stop(StopReason.UserIntention);
        _bridgeA.Stop(StopReason.UserIntention);
        _tunB.Close();
        _tunA.Close();
    }

    [Test]
    public void PacketFromA_ArrivesAtB()
    {
        byte[] packet = BuildIcmpPacket("10.99.0.1", "10.99.0.2", new byte[] { 0x01, 0x02, 0x03 });

        _tunA.Write(packet);
        Thread.Sleep(500);

        byte[] readBuf = new byte[65536];
        int bytesRead = _tunB.Read(readBuf);

        Assert.That(bytesRead, Is.GreaterThan(0));
        Assert.That(readBuf[0] >> 4, Is.EqualTo(4)); // IPv4
    }

    [Test]
    public void PacketFromB_ArrivesAtA()
    {
        byte[] packet = BuildIcmpPacket("10.99.0.2", "10.99.0.1", new byte[] { 0xAA, 0xBB });

        _tunB.Write(packet);
        Thread.Sleep(500);

        byte[] readBuf = new byte[65536];
        int bytesRead = _tunA.Read(readBuf);

        Assert.That(bytesRead, Is.GreaterThan(0));
    }

    [Test]
    public void MultiplePackets_AllDelivered_InOrder()
    {
        const int count = 50;

        for (int i = 0; i < count; i++)
        {
            byte[] packet = BuildIcmpPacket("10.99.0.1", "10.99.0.2", new byte[] { (byte)i });
            _tunA.Write(packet);
            Thread.Sleep(2);
        }

        byte[] readBuf = new byte[65536];
        int received = 0;
        var receivedPayloads = new List<byte>();

        // Read with timeout
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (received < count && DateTime.UtcNow < deadline)
        {
            int n = _tunB.Read(readBuf);
            if (n > 0)
            {
                received++;
                // Extract payload byte for ordering check
                receivedPayloads.Add(readBuf[n - 1]); // last byte of payload
            }
        }

        Assert.That(received, Is.EqualTo(count));

        for (int i = 0; i < count; i++)
        {
            Assert.That(receivedPayloads[i], Is.EqualTo((byte)i));
        }
    }
}
```

### Test 3: Client Reconnection

```csharp
[TestFixture]
[Category("Integration")]
public class ReconnectionTests
{
    [Test]
    public void ClientReconnects_AfterServerRestart()
    {
        // Start server
        // Connect client
        // Stop server
        // Verify client disconnects
        // Restart server
        // Verify client reconnects
        // Send packet through new connection
    }
}
```

### Test 4: Buffer Overflow Behavior

```csharp
[Test]
public void BufferFill_DropsNewest_AndDrainsAfterConnect()
{
    var bridge = new Bridge(..., buffer: new PacketBuffer(50_000), ...);
    bridge.Start(); // no endpoint yet

    // Fill buffer with packets until dropping starts
    for (int i = 0; i < 1000; i++)
    {
        // Inject through TUN fake/mock
        // ... after some point, drops should occur
    }

    // Connect endpoint
    // Verify buffered packets are drained
    // Verify dropped counter > 0
}
```

## IP Packet Builder Helper

```csharp
internal static class PacketBuilder
{
    /// <summary>
    /// Builds a minimal IPv4 packet with ICMP payload.
    /// Does NOT compute the correct IP checksum for now — just enough
    /// for testing the bridge data path.
    /// </summary>
    public static byte[] BuildIcmpPacket(string srcIp, string dstIp, byte[] payload)
    {
        byte[] src = ParseIp(srcIp);
        byte[] dst = ParseIp(dstIp);

        int totalLength = 20 + 8 + payload.Length; // IP header + ICMP header + payload
        var packet = new byte[totalLength];

        // IP header
        packet[0] = 0x45;       // Version=4, IHL=5
        packet[1] = 0x00;       // DSCP+ECN
        packet[2] = (byte)(totalLength >> 8);
        packet[3] = (byte)(totalLength & 0xFF);
        packet[4] = 0x00;       // ID high
        packet[5] = 0x01;       // ID low
        packet[6] = 0x00;       // Flags+Fragment
        packet[7] = 0x00;
        packet[8] = 0x40;       // TTL=64
        packet[9] = 0x01;       // Protocol=ICMP
        // Checksum at 10-11: leave 0 for now (kernel may fix or ignore)
        packet[10] = 0x00;
        packet[11] = 0x00;
        Array.Copy(src, 0, packet, 12, 4);
        Array.Copy(dst, 0, packet, 16, 4);

        // Compute IP checksum
        ushort ipChecksum = ComputeChecksum(packet, 0, 20);
        packet[10] = (byte)(ipChecksum >> 8);
        packet[11] = (byte)(ipChecksum & 0xFF);

        // ICMP header: Type=8 (Echo Request), Code=0
        packet[20] = 0x08;
        packet[21] = 0x00;
        // Checksum at 22-23: compute over ICMP header + payload
        packet[22] = 0x00;
        packet[23] = 0x00;
        packet[24] = 0x00; // ID
        packet[25] = 0x01;
        packet[26] = 0x00; // Sequence
        packet[27] = 0x01;

        Array.Copy(payload, 0, packet, 28, payload.Length);

        // Compute ICMP checksum
        ushort icmpChecksum = ComputeChecksum(packet, 20, 8 + payload.Length);
        packet[22] = (byte)(icmpChecksum >> 8);
        packet[23] = (byte)(icmpChecksum & 0xFF);

        return packet;
    }

    private static byte[] ParseIp(string ip)
    {
        var parts = ip.Split('.');
        return new byte[] {
            byte.Parse(parts[0]), byte.Parse(parts[1]),
            byte.Parse(parts[2]), byte.Parse(parts[3])
        };
    }

    private static ushort ComputeChecksum(byte[] data, int offset, int length)
    {
        uint sum = 0;
        for (int i = 0; i < length; i += 2)
        {
            ushort word = (ushort)(data[offset + i] << 8);
            if (i + 1 < length)
                word |= data[offset + i + 1];
            sum += word;
        }
        while (sum >> 16 > 0)
            sum = (sum & 0xFFFF) + (sum >> 16);
        return (ushort)~sum;
    }
}
```

## Success Criteria

1. TUN devices can be opened, closed, and reopened on a real Linux system.
2. A raw IP packet sent on one TUN arrives at the peer TUN (with correct routing).
3. Debug mode: packet written to tunA is forwarded through both bridges and arrives at tunB.
4. Debug mode: packet written to tunB is forwarded through both bridges and arrives at tunA.
5. 50+ packets are delivered in order.
6. Client reconnects after server restart.
7. Buffer overflow drops newest packets (verified by counter).
8. No crashes, no memory leaks over extended operation.

## Files to Create/Modify

| File | Action |
|------|--------|
| `tests/PigeonPost.Tests/Integration/TunDeviceIntegrationTests.cs` | Create |
| `tests/PigeonPost.Tests/Integration/DebugModeEndToEndTests.cs` | Create |
| `tests/PigeonPost.Tests/Integration/ReconnectionTests.cs` | Create |
| `tests/PigeonPost.Tests/Integration/PacketBuilder.cs` | Create |
| `docs/testing.md` | Create (test setup instructions) |
