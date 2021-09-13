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
