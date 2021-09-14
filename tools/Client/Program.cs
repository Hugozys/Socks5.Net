using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Socks5.Net;
using Socks5.Net.Extensions.Security;
using Socks5.Net.Logging;

using Socks5.Net.Security;

namespace Client
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var rootCommand = new RootCommand();
            rootCommand.Add(new Option<Mode>(new []{"--mode", "-m"}, () => Mode.PlainText, "crypto mode to operate on"));
            rootCommand.Add(new Option<int>(new []{"--port", "-p"}, () => 1080, "port on which the client will listen for forwarding traffic"));
            rootCommand.Add(new Argument<string>("hostname", "host name of the socks server that the client will forward the traffic to"));
            rootCommand.Add(new Option<int>(new []{"--sock-port", "-sp"}, () => 1080, "port on which the socks server is listening"));
            rootCommand.Add(new Option<bool>(new []{"--verbose", "-v"}, () => false, "whether to use verbose logging"));
            rootCommand.Handler = CommandHandler.Create<string, int, int, bool, Mode>(Start);
            await rootCommand.InvokeAsync(args);
        }

        public static async Task Start(string hostname, int sockPort, int port, bool verbose, Mode mode)
        {
            Socks.SetLogLevel(verbose ? LogLevel.Info: LogLevel.Error);
            var listener = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
            listener.Start();
            Console.WriteLine($"Listening on port {port}...");
            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();
                var sockServer = new TcpClient(hostname, sockPort);
                var cryptoStream = await Crypto.GetClientStreamAsync(sockServer.GetStream(), mode);
                _ = Task.Run(async () =>
                {
                    var clientStream = client.GetStream();
                    var client2ServerTunnel = clientStream.CopyToAsync(cryptoStream);
                    var server2ClientTunnel = cryptoStream.CopyToAsync(clientStream);
                    try
                    {
                        await Task.WhenAll(client2ServerTunnel, server2ClientTunnel);
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine($"Exception thrown: {ex.Message}");
                        client2ServerTunnel.Dispose();
                        server2ClientTunnel.Dispose();
                    }
                });
            }
        }
    }
}
