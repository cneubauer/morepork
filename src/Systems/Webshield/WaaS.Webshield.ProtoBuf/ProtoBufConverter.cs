using Serializer = ProtoBuf.Serializer;

namespace WaaS.Webshield.ProtoBuf;

public static class ProtoBufConverter
{
    public static byte[] ToProtoBuf<T>(this T obj)
    {
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, obj);
        return stream.ToArray();
    }

    public static T FromProtoBuf<T>(this byte[] data)
    {
        using var stream = new MemoryStream(data);
        return Serializer.Deserialize<T>(stream);
    }
}
