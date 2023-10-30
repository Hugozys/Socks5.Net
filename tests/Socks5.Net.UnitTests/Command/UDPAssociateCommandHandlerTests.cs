using System.Collections.Generic;
using FluentAssertions;
using Socks5.Net.Command;
using Socks5.Net.Common;
using Xunit;

namespace Socks5.Net.UnitTests.Command
{
    public class UDPAssociateCommandHandlerTests
    {
        [Theory]
        [ClassData(typeof(UDPDatagramErrorTestData))]
        public void UDPDatagramParseBadBytes_ShouldReturnErrorResponse(byte[] buffer, ErrorCode code)
        {
            var result = UDPDatagram.Parse(buffer);
            result.Success.Should().BeFalse();
            result.Reason.Should().Be(code);
        }

        [Theory]
        [ClassData(typeof(UDPDatagramSuccessTestData))]
        public void UDPDatagramParseGoodBytes_ShouldReturnSuccessResponse(byte[] buffer, byte addrType, byte[] ip, byte[] port, byte[] data)
        {
            var result = UDPDatagram.Parse(buffer);
            result.Success.Should().BeTrue();
            var datagram = result.Payload;
            datagram.Rsv.Should().BeEquivalentTo(new byte[] { 0x00, 0x00 });
            datagram.Frag.Should().Be(0x00);
            datagram.AddrType.Should().Be(addrType);
            datagram.DstAddr.Should().BeEquivalentTo(ip);
            datagram.DstPort.Should().BeEquivalentTo(port);
            datagram.Data.Should().BeEquivalentTo(data);
            datagram.Port.Should().Be(60000);

        }
        [Theory]
        [ClassData(typeof(UDPDatagramBufferTestData))]
        public void UDPDatagramToBytes_ShouldReturnOriginalHeader(byte[] buffer)
        {
            var result = UDPDatagram.Parse(buffer);
            result.Success.Should().BeTrue();
            var datagram = result.Payload;
            datagram.ToBytes().Should().BeEquivalentTo(buffer);
        }

        [Theory]
        [ClassData(typeof(UDPDatagramBufferTestData))]
        public void UDPDatagramCopyHeader_ShouldReturnDatagramOnlyDiffInDataProperty(byte[] buffer)
        {
            var newData = "world"u8.ToArray();
            var result = UDPDatagram.Parse(buffer);
            result.Success.Should().BeTrue();
            var datagram = result.Payload;
            var newdatagram = datagram.CopyHeader(newData);
            newdatagram.Rsv.Should().BeEquivalentTo(datagram.Rsv);
            newdatagram.Frag.Should().Be(datagram.Frag);
            newdatagram.AddrType.Should().Be(datagram.AddrType);
            newdatagram.DstAddr.Should().BeEquivalentTo(datagram.DstAddr);
            newdatagram.DstPort.Should().BeEquivalentTo(datagram.DstPort);
            newdatagram.Port.Should().Be(datagram.Port);
            newdatagram.Data.Should().BeEquivalentTo(newData);
        }

        private class UDPDatagramSuccessTestData : TheoryData<byte[], byte, byte[], byte[], byte[]>
        {
            public UDPDatagramSuccessTestData()
            {
                var testCases = GoodData();

                foreach (var (buffer, addrType, domain, data) in testCases)
                {
                    Add(buffer, addrType, domain, new byte[] { 0xea, 0x60 }, data);
                }
            }
        }

        private class UDPDatagramBufferTestData : TheoryData<byte[]>
        {
            public UDPDatagramBufferTestData()
            {
                var testCases = GoodData();
                foreach (var (buffer, _, _, _) in testCases)
                {
                    Add(buffer);
                }
            }
        }

        private static List<(byte[] buffer, byte addrType, byte[] domain, byte[] data)> GoodData() =>
            new()
            {
                    (new byte[]{
                        0x00, 0x00, // Rsv
                        0x00, // Frag
                        0x01, // IPV4
                        0xc0, 0xa8, 0x01, 0x01, // 192.168.1.1
                        0xea, 0x60,  // 60000 in big endian
                        0x68, 0x65, 0x6c, 0x6c, 0x6f // hello
                        }, 0x01, new byte[]{ 0xc0, 0xa8, 0x01, 0x01 }, "hello"u8.ToArray()),
                    (new byte[]{
                            0x00, 0x00, // Rsv
                            0x00, // Frag
                            0x04, // IPv6 
                            0x3f, 0xfe, 0x19, 0x00, 0x45, 0x45, 0x00, 0x03, 0x02, 0x00, 0xf8, 0xff, 0xfe, 0x21, 0x67, 0xcf,
                            0xea, 0x60,  // 60000 in big endian
                            0x68, 0x65, 0x6c, 0x6c, 0x6f // hello
                        },
                        0x04,
                        new byte[]{0x3f, 0xfe, 0x19, 0x00, 0x45, 0x45, 0x00, 0x03, 0x02, 0x00, 0xf8, 0xff, 0xfe, 0x21, 0x67, 0xcf },
                        "hello"u8.ToArray()),

                    (new byte[]{
                            0x00, 0x00, // Rsv
                            0x00, // Frag
                            0x03, // Domain type
                            0x0a, // domain length
                            0x67, 0x6F, 0x6F, 0x67, 0x6C, 0x65, 0x2E, 0x63, 0x6F, 0x6D, // google.com
                            0xea, 0x60, // 60000 in big endian
                            0x68, 0x65, 0x6c, 0x6c, 0x6f // hello
                        },
                        0x03,
                        "google.com"u8.ToArray(),
                        "hello"u8.ToArray())
                };
        private class UDPDatagramErrorTestData : TheoryData<byte[], ErrorCode>
        {
            public UDPDatagramErrorTestData()
            {
                // addr
                var testCaseList = new List<(byte[] buffer, ErrorCode errCode)>
                {
                    (new byte[Constants.UDPHeaderNoAddrMinLen + 2], ErrorCode.InComplete),
                    (Bytes(new byte[]{0x00, 0x01}, Constants.UDPHeaderMinLen + 1), ErrorCode.InvalidRsv),
                    (Bytes(new byte[]{0x01, 0x00}, Constants.UDPHeaderMinLen + 1), ErrorCode.InvalidRsv),
                    (Bytes(new byte[]{0x01}, Constants.UDPHeaderMinLen + 1, 2), ErrorCode.NotAllowedByRuleSet),
                    (Bytes(new byte[]{0x05}, Constants.UDPHeaderMinLen + 1, 3), ErrorCode.InvalidAddrType),
                    (Bytes(new byte[]{0x01}, Constants.UDPHeaderNoAddrMinLen + 3, 3), ErrorCode.InComplete),
                    (Bytes(new byte[]{0x04}, Constants.UDPHeaderNoAddrMinLen + 15, 3), ErrorCode.InComplete),
                    (Bytes(new byte[]{0x03, 0x05}, Constants.UDPHeaderNoAddrMinLen + 1 + 4, 3), ErrorCode.InComplete),
                };
                foreach (var (buffer, errCode) in testCaseList)
                {
                    Add(buffer, errCode);
                }
            }

            private static byte[] Bytes(byte[] init, int length, int start = 0)
            {
                var buffer = new byte[length];
                init.CopyTo(buffer, start);
                return buffer;
            }
        }

    }
}