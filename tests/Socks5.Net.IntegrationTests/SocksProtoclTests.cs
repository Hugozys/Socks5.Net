using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Socks5.Net.Command;
using Socks5.Net.Extensions.Security;
using Socks5.Net.Security;
using Xunit;

namespace Socks5.Net.IntegrationTests
{
    public class SocksProtocolTests
    {
        public SocksProtocolTests()
        {
        }

        [Theory]
        [InlineData(Mode.Xor)]
        [InlineData(Mode.Chacha20)]
        public async Task ClientSendEncryptedMessages_ServerShouldDecrptToSame(Mode mode)
        {
            var helloWorld = Encoding.Default.GetBytes("Hello World");

            async Task server(CancellationToken cancellationToken = default)
            {
                var target = new TcpListener(IPAddress.Parse("127.0.0.1"), mode switch { Mode.Xor => 9000, _ => 30000 });
                target.Start();
                var client = await target.AcceptTcpClientAsync(cancellationToken);
                var stream = await Crypto.GetServerStreamAsync(client.GetStream(), mode);
                Memory<byte> message = new byte[helloWorld.Length];
                var bytes = await stream.ReadAsync(message, cancellationToken);
                bytes.Should().Be(helloWorld.Length);
                message.ToArray().Should().BeEquivalentTo(helloWorld);
                client.Close();
                target.Stop();
                client.Dispose();
            }

            async Task client(CancellationToken cancellationToken = default)
            {
                TcpClient client;
                client = new TcpClient("127.0.0.1", mode switch { Mode.Xor => 9000, _ => 30000 });
                var stream = await Crypto.GetClientStreamAsync(client.GetStream(), mode);
                await stream.WriteAsync(helloWorld, cancellationToken);
                client.Close();
                client.Dispose();
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await Task.WhenAll(server(cts.Token), client(cts.Token));
        }

        [Fact]
        public async Task SimulateHandshake_ShouldSucceed()
        {
            var helloWorld = Encoding.Default.GetBytes("Hello World");
            var ssPort = 60000;
            var sPort = 60002;
            async Task sockServer(CancellationToken cancellationToken = default)
            {
                var sock = new TcpListener(IPAddress.Parse("127.0.0.1"), ssPort);
                sock.Start();
                var client = await sock.AcceptTcpClientAsync(cancellationToken);
                var stream = client.GetStream();
                using var sockConnect = Socks.CreateSock(stream, stream.Socket.RemoteEndPoint!);
                await sockConnect.ServeAsync(cancellationToken);
                client.Close();
                sock.Stop();
                client.Dispose();
            }

            async Task sockClient(CancellationToken cancellationToken = default)
            {
                TcpClient client;
                client = new TcpClient("127.0.0.1", ssPort);
                var stream = client.GetStream();
                await stream.WriteAsync(new byte[] { 0x05, 0x01, 0x00 }, cancellationToken);
                Memory<byte> buffer = new byte[2];
                var bytes = await stream.ReadAsync(buffer, cancellationToken);

                bytes.Should().Be(2);
                var expected = new byte[] { 0x05, 0x00 };
                buffer.ToArray().Should().BeEquivalentTo(expected);

                Memory<byte> port = new byte[4];
                var beInt = IPAddress.HostToNetworkOrder(sPort);
                BitConverter.TryWriteBytes(port.Span, beInt);
                var portArray = port.ToArray();
                var byteArray = new List<byte>
                {
                    0x05,
                    0x01,
                    0x00,
                    0x03,
                    0x09,
                    0x6c, 0x6f, 0x63, 0x61, 0x6c, 0x68, 0x6f, 0x73, 0x74, //localhost
                    portArray[2],
                    portArray[3]
                };
                await stream.WriteAsync(byteArray.ToArray(), cancellationToken);
                Memory<byte> reply = new byte[50];
                await stream.ReadAsync(reply, cancellationToken);
                reply.ToArray()[1].Should().Be(0x00);

                await stream.WriteAsync(helloWorld, cancellationToken);
                client.Close();
            }

            async Task targetHost(CancellationToken cancellationToken = default)
            {
                var target = new TcpListener(IPAddress.Parse("127.0.0.1"), sPort);
                target.Start();
                using var client = await target.AcceptTcpClientAsync(cancellationToken);
                var stream = client.GetStream();
                Memory<byte> text = new byte[helloWorld.Length];
                var readBytes = await stream.ReadAsync(text, cancellationToken);
                readBytes.Should().Be(helloWorld.Length);
                text.ToArray().Should().BeEquivalentTo(helloWorld);
                client.Close();
                target.Stop();
                client.Dispose();
            }
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

            await Task.WhenAll(sockServer(cts.Token), sockClient(cts.Token), targetHost(cts.Token));
        }

        [Fact]
        public async Task SimulateUDPAssociate_ShouldSucceed()
        {
            var helloWorld = Encoding.Default.GetBytes("Hello World");
            var ssPort = 60000;
            var cuPort = 63000;
            var ruPort = 65000;
            async Task sockServer(CancellationToken cancellationToken = default)
            {
                var sock = new TcpListener(IPAddress.Parse("127.0.0.1"), ssPort);
                sock.Start();
                using var client = await sock.AcceptTcpClientAsync(cancellationToken);
                var stream = client.GetStream();
                using var sockConnect = Socks.CreateSock(
                    stream,
                    stream.Socket.RemoteEndPoint!,
                    new SocksOption
                    {
                        UDPRelayAddr = IPAddress.Loopback
                    });
                await sockConnect.ServeAsync(cancellationToken);
                client.Close();
                sock.Stop();
            }

            async Task sockClient(CancellationToken cancellationToken = default)
            {
                TcpClient client;
                client = new TcpClient("127.0.0.1", ssPort);
                var stream = client.GetStream();
                // Version identifier/method selection
                await stream.WriteAsync(new byte[] { 0x05, 0x01, 0x00 }, cancellationToken);
                Memory<byte> buffer = new byte[2];

                // Receive selected method
                var bytes = await stream.ReadAsync(buffer, cancellationToken);
                bytes.Should().Be(2);
                var expected = new byte[] { 0x05, 0x00 };
                buffer.ToArray().Should().BeEquivalentTo(expected);

                Memory<byte> port = new byte[4];
                // Client will send UDP relay from port 63000
                var beInt = IPAddress.HostToNetworkOrder(cuPort);
                BitConverter.TryWriteBytes(port.Span, beInt);
                var portArray = port.ToArray();
                var byteArray = new List<byte>
                {
                    0x05,
                    0x03,
                    0x00,
                    0x03,
                    0x09,
                    0x6c, 0x6f, 0x63, 0x61, 0x6c, 0x68, 0x6f, 0x73, 0x74, //localhost
                    portArray[2],
                    portArray[3]
                };
                // Send UDP Associate request
                await stream.WriteAsync(byteArray.ToArray(), cancellationToken);

                // Read Socks server reply
                Memory<byte> reply = new byte[50];
                var readBytes = await stream.ReadAsync(reply, cancellationToken);
                var replyBuffer = reply[..readBytes];
                readBytes.Should().Be(10);
                replyBuffer[..8].ToArray().Should().BeEquivalentTo(new byte[] {
                     0x05, // Socks5
                     0x00, // Success
                     0x00, // Rsv
                     0x01, // IPV4
                     0x7f, 0x00, 0x00, 0x01 // 127.0.0.1 
                    });

                // Server asks the client to send encapsulated UDP relay to the following ip and port.

                var udpRelayPort = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(replyBuffer.Span[8..].ToArray()));
                var remoteEndpoint = new IPEndPoint(IPAddress.Loopback, udpRelayPort);

                using var relayClient = new UdpClient(new IPEndPoint(IPAddress.Loopback, cuPort));
                relayClient.Connect(remoteEndpoint);

                // Build UDP packet to send
                var bufferList = new List<byte[]>{
                    new byte[]{0x00, 0x00, 0x00, 0x01, 0x7f, 0x00, 0x00, 0x01 },
                    BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)ruPort)),
                    helloWorld,
                };
                // Send relay packet to Socks server
                var sendData = bufferList.SelectMany(x => x).ToArray();
                var sentBytes = await relayClient.SendAsync(sendData, cancellationToken);
                sentBytes.Should().Be(sendData.Length);

                // Receive relay packet back from Socks server
                var result = await relayClient.ReceiveAsync(cancellationToken);
                var response = UDPDatagram.Parse(result.Buffer);
                response.Success.Should().BeTrue();
                var datagram = response.Payload;
                datagram.Rsv.Should().BeEquivalentTo(new byte[] { 0x00, 0x00 });
                datagram.Frag.Should().Be(0x00);
                datagram.AddrType.Should().Be((byte)AddressType.IPV4);
                datagram.DstAddr.Should().BeEquivalentTo(new byte[] { 0x7f, 0x00, 0x00, 0x01 });
                datagram.Port.Should().Be(ruPort);
                datagram.Data.Should().BeEquivalentTo(helloWorld);

                relayClient.Close();
                client.Close();
            }

            async Task targetHost(CancellationToken cancellationToken = default)
            {
                using var target = new UdpClient(new IPEndPoint(IPAddress.Parse("127.0.0.1"), ruPort));
                var result = await target.ReceiveAsync(cancellationToken);
                result.Buffer.Length.Should().Be(helloWorld.Length);
                result.Buffer.Should().BeEquivalentTo(helloWorld);

                var sentBytes = await target.SendAsync(result.Buffer, result.RemoteEndPoint, cancellationToken);
                sentBytes.Should().Be(helloWorld.Length);
                target.Close();
            }
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

            await Task.WhenAll(targetHost(cts.Token), sockServer(cts.Token), sockClient(cts.Token));
        }
    }
}
