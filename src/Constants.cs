using System.Collections.Generic;
using System.Collections.Immutable;

namespace Sock5.Net
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

        public static class AuthMethods
        {
            public const byte NoAuth = 0x00; 
            public const byte GSSAPI = 0x01;
            public const byte UserNameAndPassword = 0x02;
            public const byte NoAccept = 0xFF;
        }

         public static readonly ImmutableHashSet<byte> PreDefinedAuthMethods = new HashSet<byte>(){
            AuthMethods.NoAuth, 
            AuthMethods.UserNameAndPassword, 
            AuthMethods.GSSAPI
         }.ToImmutableHashSet(); 

        public static class CMD
        {
            public const byte Connect = 0x01;
            public static readonly ImmutableHashSet<byte> CmdSet = new HashSet<byte> { Connect }.ToImmutableHashSet();
        }

        public static class AddrType
        {
            public const byte IPV4 = 0x01;
            public const byte Domain = 0x03;
            public const byte IPV6 = 0x04;
            public static readonly ImmutableHashSet<byte> AddrTypeSet = new HashSet<byte> { IPV4, Domain, IPV6 }.ToImmutableHashSet();
        }
    }
}
