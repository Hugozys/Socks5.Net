using System;

namespace Socks5.Net.UnitTests.TestHelper
{
    public static class IPAddress
    {
        public static ushort HostToNetworkOrder(ushort value)
        {
            Span<byte> bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                var temp = bytes[0];
                bytes[0] = bytes[1];
                bytes[1] = temp;
                return BitConverter.ToUInt16(bytes);
            }
            return value;
        }
    }
}