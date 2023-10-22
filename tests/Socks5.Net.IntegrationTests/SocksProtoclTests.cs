using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
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

            var target = new TcpListener(IPAddress.Parse("127.0.0.1"), mode switch {Mode.Xor => 9000, _ => 30000});
            target.Start();

            async Task server()
            {
                var client = await target.AcceptTcpClientAsync();
                var stream = await Crypto.GetServerStreamAsync(client.GetStream(), mode);
                Memory<byte> message = new byte[helloWorld.Length];
                var bytes =  await stream.ReadAsync(message);
                bytes.Should().Be(helloWorld.Length);
                message.ToArray().Should().BeEquivalentTo(helloWorld);
                client.Close();
                target.Stop();
                client.Dispose();
            }
            
            async Task client()
            {
                TcpClient client;
                client = new TcpClient("127.0.0.1", mode switch {Mode.Xor => 9000, _ => 30000});
                var stream = await Crypto.GetClientStreamAsync(client.GetStream(), mode);
                await stream.WriteAsync(helloWorld);
                client.Close();
                client.Dispose();
            }
               
            await Task.WhenAll(server(), client());
        }

        [Fact]
        public async Task SimulateHandshake_ShouldSucceed()
        {
            var helloWorld = Encoding.Default.GetBytes("Hello World");

            var target = new TcpListener(IPAddress.Parse("127.0.0.1"), 8080);
            target.Start();

            var sock = new TcpListener(IPAddress.Parse("127.0.0.1"), 5000);
            sock.Start();

            async Task sockServer()
            {
                var client = await sock.AcceptTcpClientAsync();
                var stream = client.GetStream();
                using var sockConnect = Socks.CreateSock(stream);
                await sockConnect.ServeAsync();
                client.Close();
                sock.Stop();
                client.Dispose();
            }
            
            async Task sockClient()
            {
                TcpClient client;
                client = new TcpClient("127.0.0.1", 5000);
                var stream = client.GetStream();
                await stream.WriteAsync(new byte[]{0x05, 0x01, 0x00});
                Memory<byte> buffer = new byte[2];
                var bytes = await stream.ReadAsync(buffer);

                bytes.Should().Be(2);
                var expected = new byte[]{0x05, 0x00};
                buffer.ToArray().Should().BeEquivalentTo(expected);
                
                Memory<byte> port = new byte[4];
                var beInt = IPAddress.HostToNetworkOrder(8080);
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
                await stream.WriteAsync(byteArray.ToArray());
                Memory<byte> reply = new byte[50];
                await stream.ReadAsync(reply);
                reply.ToArray()[1].Should().Be(0x00);

                await stream.WriteAsync(helloWorld);
                client.Close();
                client.Dispose();
            }

            async Task targetHost()
            {
                using var client = await target.AcceptTcpClientAsync();
                var stream = client.GetStream();
                Memory<byte> text = new byte[helloWorld.Length];
                var readBytes = await stream.ReadAsync(text);
                readBytes.Should().Be(helloWorld.Length);
                text.ToArray().Should().BeEquivalentTo(helloWorld);
                client.Close();
                target.Stop();
                client.Dispose();
            }
            await Task.WhenAny(sockServer(), sockClient(), targetHost());
        }
    }
}
