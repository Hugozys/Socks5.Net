using System.Collections.Generic;
using Sock5.Net.Common;

namespace Sock5.Net
{
    public class SockOption
    {
        public List<byte> SupportedAuthMethods = new() { Constants.AuthMethods.NoAuth };
    }
}