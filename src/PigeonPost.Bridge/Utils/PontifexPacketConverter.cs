using Actuarius.Memory;
using Pontifex.Utils;

namespace PigeonPost.Bridge.Utils;

internal static class PontifexPacketConverter
{
    public static UnionDataList CreateMessage(byte[] packet)
    {
        var list = new UnionDataList();
        list.PutFirst(new UnionData(new StaticReadOnlyByteArray(packet)));
        return list;
    }

    public static byte[] ExtractPacket(IMultiRefReadOnlyByteArray data)
    {
        int count = data.Count;
        byte[] copy = new byte[count];
        data.CopyTo(copy, 0, 0, count);
        return copy;
    }
}
