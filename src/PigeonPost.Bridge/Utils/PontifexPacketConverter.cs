using Actuarius.Memory;
using Pontifex.Utils;

namespace PigeonPost.Bridge;

internal static class PontifexPacketConverter
{
    public static UnionDataList CreateMessage(byte[] packet)
    {
        var list = new UnionDataList();
        list.PutFirst(new UnionData(new StaticReadOnlyByteArray(packet)));
        return list;
    }
}
