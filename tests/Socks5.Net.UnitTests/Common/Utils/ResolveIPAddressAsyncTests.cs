using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Socks5.Net.Common;
using Xunit;

namespace Socks5.Net.UnitTests.Common
{
    public class ResolveIPAddressAsyncTests
    {
        [Fact]
        public async Task InvalidHostName_Failed()
        {
            var payload = Encoding.Default.GetBytes("/*-");
            var result = await Utils.ResolveIPAddressAsync((byte)AddressType.Domain, payload);
            result.Success.Should().BeFalse();
            result.Reason.Should().Be(ErrorCode.InvalidHostName);
        }

        [Fact]
        public async Task UnreachableHostName_Failed()
        {
            var payload = Encoding.Default.GetBytes("aaa");
            var result = await Utils.ResolveIPAddressAsync((byte)AddressType.Domain, payload);
            result.Success.Should().BeFalse();
            result.Reason.Should().Be(ErrorCode.UnreachableHost);
        }
        [Theory]
        [InlineData(AddressType.Domain, "www.google.com")]
        [InlineData(AddressType.Domain, "142.250.188.36")]
        [InlineData(AddressType.IPV4, "142.250.188.36")]
        [InlineData(AddressType.IPV6, "2607:f8b0:4004:0835:0000:0000:0000:2004")]
        public async Task ValidBytes_Resolved(AddressType type, string payload)
        {
            var bytes = type switch
            {
                AddressType.Domain => Encoding.Default.GetBytes(payload),
                AddressType.IPV4 => payload.Split('.').Select(x => Convert.ToByte(x)).ToArray(),
                _ => payload.Split(':').SelectMany(x => new byte[] { Convert.ToByte(x[..2], 16), Convert.ToByte(x[2..], 16) }).ToArray()
            };
            var result = await Utils.ResolveIPAddressAsync((byte)type, bytes);
            result.Success.Should().BeTrue();
            result.Payload.Should().NotBeNull();
        }
    }
}