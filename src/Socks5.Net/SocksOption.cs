using System.Collections.Generic;
using Socks5.Net.Common;

namespace Socks5.Net
{
    public class SocksOption
    {
        public List<AuthenticationMethod> SupportedAuthMethods = new() { AuthenticationMethod.NoAuth };
    }
}