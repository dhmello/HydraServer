using System.Buffers.Binary;

namespace HydraServer;

internal struct PacketReader
{
    private byte[] _data;
    private int _position;

    public PacketReader(byte[] data)
    {
        _data = data;
        _position = 0;
    }

    public bool Eof => _position >= _data.Length;

    public ushort ReadU16()
    {
        var v = BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(_position, 2));
        _position += 2;
        return v;
    }

    public uint ReadU32()
    {
        var v = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(_position, 4));
        _position += 4;
        return v;
    }

    public byte ReadU8()
    {
        var v = _data[_position];
        _position += 1;
        return v;
    }

    public byte[] ReadBytes(int len)
    {
        var result = new byte[len];
        Buffer.BlockCopy(_data, _position, result, 0, len);
        _position += len;
        return result;
    }
}

internal sealed class PacketWriter
{
    private readonly System.IO.MemoryStream _ms = new();

    public void U8(byte v) => _ms.WriteByte(v);

    public void U16(ushort v)
    {
        Span<byte> b = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(b, v);
        _ms.Write(b);
    }

    public void U32(uint v)
    {
        Span<byte> b = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, v);
        _ms.Write(b);
    }

    public void Bytes(byte[] s) => _ms.Write(s, 0, s.Length);

    public void Bytes(ReadOnlySpan<byte> s) => _ms.Write(s);

    public byte[] ToArray() => _ms.ToArray();
}