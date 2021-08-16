using System;
using System.Threading.Tasks;
using FluentAssertions;
using static Sock5.Net.UnitTests.TestHelper.PipeStream;
using Xunit;

namespace Sock5.Net.UnitTests
{
    public class ReadAuthMethodsAsyncTests
    {

        // failure scenario:
        // 1. invalid version number
        // 2. incomplete message
        // 3. invalid nmethods

        // happy path
        // 1. correct payload
        // 2. mimic delay scenario 
        [Fact]
        public async Task InvalidVersionNumber_Failed()
        {
            Memory<byte> payload = new byte[2] { 0x00, 0x01 };
            
            var sock = CreatePipeFromRStream(payload);

            var result = await sock.Reader.ReadAuthMethodsAsync();

            result.Success.Should().BeFalse();
            result.Reason.Should().Be(ErrorCode.InvalidVersionNumber);
        }

        [Theory]
        [InlineData((byte)0x05)]
        [InlineData((byte)0x05, (byte)0x03, (byte)0x04, (byte)0x07)]
        public async Task Incomplete_Failed(params byte[] payload)
        {
            var sock = CreatePipeFromRStream(payload.AsMemory());

            var result = await sock.Reader.ReadAuthMethodsAsync();

            result.Success.Should().BeFalse();
            result.Reason.Should().Be(ErrorCode.InComplete);
        }

        [Fact]
        public async Task InvalidNMethods_Failed()
        {
            Memory<byte> payload = new byte[2] { 0x05, 0x00 };
            
            var sock = CreatePipeFromRStream(payload);

            var result = await sock.Reader.ReadAuthMethodsAsync();

            result.Success.Should().BeFalse();

            result.Reason.Should().Be(ErrorCode.InvalidNMethods);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CorrectPayload_ReturnIntersectedMethods(bool delayed)
        {
            Memory<byte> payload = new byte[5] 
            { 
                Constants.Version, 
                0x03, 
                Constants.AuthMethods.NoAuth, 
                Constants.AuthMethods.GSSAPI,
                Constants.AuthMethods.UserNameAndPassword 
            };
            
            var sock = CreatePipeFromRStream(payload, delayed);

            var result = await sock.Reader.ReadAuthMethodsAsync();

            result.Success.Should().BeTrue();
            result.Payload.Count.Should().Be(3);
        }


        
    }
}
