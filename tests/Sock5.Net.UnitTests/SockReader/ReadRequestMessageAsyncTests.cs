using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using static Sock5.Net.UnitTests.TestHelper.PipeStream;
using Sock5.Net.Common;

namespace Sock5.Net.UnitTests
{
    public class ReadRequestMessageAsyncTests
    {
        // failure scenario:
        // 1. invalid version number
        // 2. invalid cmd
        // 3. invalid rsv
        // 4. invalid address type
        // 5. incomplete message

        // happy path
        // 1. correct payload
        // 2. mimic delay scenario 
        [Theory]
        [InlineData(ErrorCode.InvalidVersionNumber, (byte)0x00)]
        [InlineData(ErrorCode.InComplete, (byte)0x05)]
        public async Task VersionNumber_Failed(ErrorCode code, params byte[] payload)
        {
            var sock = CreatePipeFromRStream(payload);

            var result = await sock.Reader.ReadRequestMessageAsync();

            result.Success.Should().BeFalse();
            result.Reason.Should().Be(code);
        }

        [Theory]
        [InlineData(ErrorCode.InvalidCmd, (byte)0x05, (byte)0x04)]
        [InlineData(ErrorCode.InComplete, (byte)0x05, (byte)0x01)]
        public async Task CMD_Failed(ErrorCode code, params byte[] payload)
        {
            var sock = CreatePipeFromRStream(payload.AsMemory());

            var result = await sock.Reader.ReadRequestMessageAsync();

            result.Success.Should().BeFalse();
            result.Reason.Should().Be(code);
        }

        [Theory]
        [InlineData(ErrorCode.InvalidRsv, (byte)0x05, (byte)0x01, (byte)0x01)]
        [InlineData(ErrorCode.InComplete, (byte)0x05, (byte)0x01, (byte)0x00)]
        public async Task RSV_Failed(ErrorCode code, params byte[] payload)
        {
            var sock = CreatePipeFromRStream(payload);

            var result = await sock.Reader.ReadRequestMessageAsync();

            result.Success.Should().BeFalse();

            result.Reason.Should().Be(code);
        }

        [Theory]
        [InlineData(ErrorCode.InvalidAddrType, (byte)0x05, (byte)0x01, (byte)0x00, (byte)0x02)]
        [InlineData(ErrorCode.InComplete, (byte)0x05, (byte)0x01, (byte)0x00, (byte)0x03)]
        public async Task ATYP_Failed(ErrorCode code, params byte[] payload)
        {
            var sock = CreatePipeFromRStream(payload);

            var result = await sock.Reader.ReadRequestMessageAsync();

            result.Success.Should().BeFalse();

            result.Reason.Should().Be(code);
        }

        [Theory]
        [ClassData(typeof(TestData))]
        public async Task DST_ADDR_Failed(ErrorCode code, params byte[] payload)
        {
            var sock = CreatePipeFromRStream(payload);

            var result = await sock.Reader.ReadRequestMessageAsync();

            result.Success.Should().BeFalse();

            result.Reason.Should().Be(code);
        }

        [Fact]
        public async Task DST_PORT_Failed()
        {
            var payload = new byte[6] { 0x05, 0x01, 0x00, 0x01, 0x42, 0x03 };
            var sock = CreatePipeFromRStream(payload);
            var result = await sock.Reader.ReadRequestMessageAsync();
            result.Success.Should().BeFalse();
            result.Reason.Should().Be(ErrorCode.InComplete);
        }

        private class TestData : TheoryData<ErrorCode, byte[]>
        {
            public TestData()
            {
                foreach (var atyp in Constants.AddrType.AddrTypeSet)
                {
                    Add(ErrorCode.InComplete, new byte[] { 0x05, 0x01, 0x00, atyp, 0x01 });
                }
            }
        }

        [Theory]
        [ClassData(typeof(CorrectPayloadTestData))]
        public async Task CorrectPayload_ReturnRequestMessage(byte[] payload, bool delayed, RequestMessage expected)
        {
            var sock = CreatePipeFromRStream(payload, delayed);

            var result = await sock.Reader.ReadRequestMessageAsync();

            result.Success.Should().BeTrue();

            /* To determine whether Fluent Assertions should recurs into an object’s properties or fields, 
             * it needs to understand what types have value semantics and what types should be treated as reference types. 
             * The default behavior is to treat every type that overrides Object.Equals as an object that was designed to have value semantics. 
             * Anonymous types, records and tuples also override this method, 
             * but because the community proved us that they use them quite often in equivalency comparisons,
             * we decided to always compare them by their members.
            */
            result.Payload.Should().BeEquivalentTo(expected, opt => opt.ComparingByMembers<RequestMessage>());
        }

        private class CorrectPayloadTestData : TheoryData<byte[], bool, RequestMessage>
        {
            public CorrectPayloadTestData()
            {
                // addr
                const string ipv4 = "127.0.0.1";
                const string ipv6 = "2001:0db8:3333:4444:5555:6666:7777:8888";
                var ipv4b = ipv4.Split('.').Select(x => Convert.ToByte(x)).ToArray();
                var ipv6b = ipv6.Split(':').SelectMany(x => new byte[] { Convert.ToByte(x.Substring(0, 2), 16), Convert.ToByte(x.Substring(2), 16) }).ToArray();
                var addrs = new List<(byte typ, byte[] payload, byte[] expected)> { (Constants.AddrType.IPV4, ipv4b, ipv4b), (Constants.AddrType.IPV6, ipv6b, ipv6b) };

                var domains = new List<string>() { "www.google.com", new string('a', 255), string.Empty }.Select(x =>
                   {
                       var len = Convert.ToByte(x.Length);
                       Span<byte> hostbytes = Encoding.Default.GetBytes(x);
                       Span<byte> span = new byte[x.Length + 1];
                       span[0] = len;
                       var subspan = span[1..];
                       hostbytes.CopyTo(subspan);
                       return (typ: Constants.AddrType.Domain, payload: span.ToArray(), expected: hostbytes.ToArray());
                   });
                addrs.AddRange(domains);

                // port
                var ports = new List<ushort>() { 0, 1080, 8080, 65535 }.Select(x =>
                {
                    var sInt = BitConverter.ToInt16(BitConverter.GetBytes(x));
                    var netByteOrder = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(sInt));
                    return (payload: netByteOrder, expected: (int)x);
                });

                foreach (var addr in addrs)
                {
                    foreach (var port in ports)
                    {
                        var byteSequence = new List<byte[]>() { new byte[] { 0x05, 0x01, 0x00 } };
                        byteSequence.Add(new byte[]{addr.typ});
                        byteSequence.Add(addr.payload);
                        byteSequence.Add(port.payload);
                        var result = new RequestMessage.Builder()
                            .WithCmd(0x01)
                            .WithAddrType(addr.typ)
                            .WithHost(addr.expected)
                            .WithPort(port.expected)
                            .ToRequestMessage();
                        var payload = byteSequence.SelectMany(x => x).ToArray();
                        Add(payload, false, result);
                        Add(payload, true, result);
                    }
                }
            }
        }

    }
}