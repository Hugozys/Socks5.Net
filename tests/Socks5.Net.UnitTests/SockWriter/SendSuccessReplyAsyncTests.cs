using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using static Socks5.Net.UnitTests.TestHelper.PipeStream;

namespace Socks5.Net.UnitTests
{
    public class SendSuccessReplyAsyncTests
    {
        [Theory]
        [InlineData("2001:0db8:3333:4444:5555:6666:7777:8888", true)]
        [InlineData("127.0.0.1", false)]
        public async Task SendSuccessReplyAsync_IPV6(string ipStr, bool delayed)
        {
            var ipendpoint = ConvertToIPEndpoint(ipStr, 65535, out var expected);
            var (pipe, stream) = CreatePipeFromRWStream(delayed);
            var response = await pipe.Writer.SendSuccessReplyAsync(ipendpoint);
            response.Success.Should().BeTrue();
            stream.Position = 0;
            var result = new byte[expected.Length];
            int count = await stream.ReadAsync(result);
            count.Should().Be(expected.Length);
            result.Should().BeEquivalentTo(expected);

        }

        private static IPEndPoint ConvertToIPEndpoint(string ipaddress, int port, out byte[] bytes)
        {
            byte[] ipBytes = null;
            byte ipType;
            if (ipaddress.IndexOf(':') != -1)
            {
                ipBytes = ipaddress.Split(':').SelectMany(x => new byte[] { Convert.ToByte(x.Substring(0, 2), 16), Convert.ToByte(x.Substring(2), 16) }).ToArray();
                ipType = (byte)AddressType.IPV6;
            }
            else
            {
                ipBytes = ipaddress.Split('.').Select(x => Convert.ToByte(x)).ToArray();
                ipType = (byte)AddressType.IPV4;
            }

            var portBytes = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(port))[^2..];
            bytes = new List<byte[]>
            {
                new byte[]{0x05, 0x00, 0x00, ipType},
                ipBytes,
                portBytes
            }.SelectMany(x => x).ToArray();
            var ip = IPAddress.Parse(ipaddress);
            return new IPEndPoint(ip, port);
        }

    }
}