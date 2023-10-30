using System;

namespace Socks5.Net.Common
{
    public readonly struct RequestMessage
    {
        public byte CmdType { get; }

        public byte AddrType { get; }

        public byte[] Host { get; }

        public int Port { get; }

        public class Builder
        {
            public byte? Cmd { get; private set; }
            public byte? AddrType { get; private set; }
            public byte[]? Host { get; private set; }
            public int? Port { get; private set; }

            public Builder()
            {
            }

            public Builder WithCmd(byte cmd) { Cmd = cmd; return this; }
            public Builder WithAddrType(byte addrType) { AddrType = addrType; return this; }
            public Builder WithHost(byte[] host) { Host = host; return this; }
            public Builder WithPort(int port) { Port = port; return this; }

            public RequestMessage ToRequestMessage()
            {
                var result = new RequestMessage(
                    Cmd ?? throw new ArgumentNullException(nameof(Cmd)),
                    AddrType ?? throw new ArgumentNullException(nameof(AddrType)),
                    Host ?? throw new ArgumentNullException(nameof(Host)),
                    Port ?? throw new ArgumentNullException(nameof(Port)));
                Clear();
                return result;
            }

            public void Clear()
            {
                Cmd = null;
                AddrType = null;
                Host = null;
                Port = null;
            }
        }

        private RequestMessage(byte cmdType, byte addrType, byte[]? host, int port)
        {
            CmdType = cmdType;
            AddrType = addrType;
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Port = port;
        }
    }

    public sealed class DummyRequestMessage
    {
        private static readonly RequestMessage instance =
            new RequestMessage.Builder().
                                WithCmd(0).
                                WithAddrType((byte)AddressType.IPV4).
                                WithHost(new byte[4]).
                                WithPort(0).
                                ToRequestMessage();

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static DummyRequestMessage()
        {
        }

        private DummyRequestMessage()
        {
        }

        public static RequestMessage Instance
        {
            get
            {
                return instance;
            }
        }
    }
}