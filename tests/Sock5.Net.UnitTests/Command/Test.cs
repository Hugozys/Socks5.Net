using System;
using System.Net;
using Xunit;

namespace Sock5.Net.UnitTests.Command
{
    public class Test
    {
        public Test()
        {
        }

        [Fact]
        public void Method()
        {
            var ip = IPAddress.Parse("2001:db8:3333:4444:5555:6666:7777:8888");
            var port = BitConverter.GetBytes(IPAddress.HostToNetworkOrder((long)8080));
        }
    }
}