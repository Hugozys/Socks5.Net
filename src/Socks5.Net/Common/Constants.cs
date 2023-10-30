using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Socks5.Net.Common
{
    public static class Constants
    {
        public const byte Version = 0x05;

        public const byte Rsv = 0x00;

        public const byte Frag = 0x00;

        // RSV - 2 bytes
        // FRAG - 1 byte
        // AddrType - 1 byte
        // Addrlen >= 1 byte
        //  - IPV4 4 bytes
        //  - IPV6 12 bytes
        //  - Domain Name 1 + ? bytes
        // Port - 2 bytes
        public const int UDPHeaderMinLen = 7;

        public const int UDPHeaderNoAddrMinLen = 4;
        public static readonly ImmutableDictionary<byte, string> ReplyCodes = new Dictionary<byte, string>
        {
            [(byte)ReplyOctet.Succeed] = "succeeded",
            [(byte)ReplyOctet.GeneralFailure] = "general SOCKS server failure",
            [(byte)ReplyOctet.ConnectionNotAllowed] = "connection not allowed by ruleset",
            [(byte)ReplyOctet.NetworkUnreachable] = "Network unreachable",
            [(byte)ReplyOctet.HostUnreachable] = "Host unreachable",
            [(byte)ReplyOctet.ConnectionRefused] = "Connection refused",
            [(byte)ReplyOctet.TTLExpired] = "TTL expired",
            [(byte)ReplyOctet.CMDNotSupported] = "Command not supported",
            [(byte)ReplyOctet.AddrTypeNotSupported] = "Address type not supported"
        }.ToImmutableDictionary();

        public static readonly ImmutableHashSet<AuthenticationMethod> AuthenticationMethodEnumSet = Enum.GetValues<AuthenticationMethod>().Where(x => x is not AuthenticationMethod.NoAccept).ToImmutableHashSet();
        public static readonly ImmutableHashSet<byte> AuthenticationMethodByteSet = AuthenticationMethodEnumSet.Select(x => (byte)x).ToImmutableHashSet();
        public static readonly ImmutableHashSet<CommandType> CommandTypeEnumSet = Enum.GetValues<CommandType>().Where(x => x is not CommandType.Unsupported).ToImmutableHashSet();
        public static readonly ImmutableHashSet<byte> CommandTypeByteSet = CommandTypeEnumSet.Select(x => (byte)x).ToImmutableHashSet();
        public static readonly ImmutableHashSet<AddressType> AddressTypeEnumSet = Enum.GetValues<AddressType>().Where(x => x is not AddressType.Unsupported).ToImmutableHashSet();
        public static readonly ImmutableHashSet<byte> AddressTypeByteSet = AddressTypeEnumSet.Select(x => (byte)x).ToImmutableHashSet();
    }
}
