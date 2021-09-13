using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using FluentAssertions;
using Socks5.Net.Pipe;
using Xunit;
using static Socks5.Net.UnitTests.TestHelper.PipeStream;
using Socks5.Net.Common;

namespace Socks5.Net.UnitTests
{
    public class SendSelectedAuthMethodAsyncTests
    {

        [Theory]
        [ClassData(typeof(TestData))]
        public async Task SendSelectedAuthMethods_CanReadSameBytesFromStream(ImmutableHashSet<byte> authSet, bool delayed, byte[] expected)
        {
            var (pipe, stream) = CreatePipeFromRWStream(delayed);
            var result = await pipe.Writer.SendSelectedAuthMethodAsync(authSet, new SocksOption());
            result.Success.Should().BeTrue();
            result.Payload.Should().Be(expected[1]);
            stream.Position = 0;
            var bytes = new byte[2];
            var read = await stream.ReadAsync(bytes);
            read.Should().Be(2);
            bytes[0].Should().Be(expected[0]);
            bytes[1].Should().Be(expected[1]);
        }

        private class TestData: TheoryData<ImmutableHashSet<byte>, bool, byte[]>
        {
            public TestData()
            {
                var intersect = new HashSet<byte>(){(byte)AuthenticationMethod.NoAuth }.ToImmutableHashSet();
                var zero = new HashSet<byte>(){(byte)AuthenticationMethod.GSSAPI }.ToImmutableHashSet();
                var noaccept = new byte[2]{ Constants.Version, (byte)AuthenticationMethod.NoAccept };
                var accept = new byte[2]{ Constants.Version, (byte)AuthenticationMethod.NoAuth };
                Add(intersect, true, accept);
                Add(intersect, false, accept);
                Add(zero, true, noaccept);
                Add(zero, false, noaccept);
            }
        }
        
        
        
    }
}