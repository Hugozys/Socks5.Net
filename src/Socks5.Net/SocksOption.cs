using System.Collections.Generic;
using System.Net;
using Socks5.Net.Common;

namespace Socks5.Net
{
    public class SocksOption
    {
        public List<AuthenticationMethod> SupportedAuthMethods = new() { AuthenticationMethod.NoAuth };
        public IPAddress UDPRelayAddr = IPAddress.Any;
    }
}