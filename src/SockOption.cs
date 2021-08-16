using System.Collections.Generic;

namespace Sock5.Net
{
    public class SockOption
    {
        public List<byte> SupportedAuthMethods = new List<byte> { Constants.AuthMethods.NoAuth };
    }
}