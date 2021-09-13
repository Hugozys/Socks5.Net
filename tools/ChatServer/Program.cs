using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Socks5.Net;
using Socks5.Net.Logging;
using Socks5.Net.Security;
using System;
using System.CommandLine;
using Socks5.Net.Extensions.Security;
using System.CommandLine.Invocation;

namespace ChatServer
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            Socks.SetLogLevel(LogLevel.Info);
            var rootCommand = new RootCommand();
            rootCommand.Add(new Option<Mode>(new []{"--mode", "-m"}, () => Mode.PlainText, "crypto mode to operate on"));
            rootCommand.Add(new Option<int>(new []{"--port", "-p"}, () => 8080, "port on which the socks server will listen for traffic"));
            rootCommand.Add(new Option<bool>(new []{"--verbose", "-v"}, () => false, "whether to use verbose logging"));
            rootCommand.Handler = CommandHandler.Create<Mode, int, bool>(Start);
            await rootCommand.InvokeAsync(args);
        }

        public static async Task Start(Mode mode, int port, bool verbose)
        {
            Socks.SetLogLevel(verbose ? LogLevel.Info: LogLevel.Error);
            var listener = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
            Console.WriteLine("Listening on port {port}..");
            listener.Start();
            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();
                Console.WriteLine("Received connection from client");
                var cryptoStream = await Crypto.GetServerStreamAsync(client.GetStream(), mode);
                _ = Task.Run(async () =>
                {
                    var stdin = Console.OpenStandardInput();
                    var stdout = Console.OpenStandardOutput();
                    var stdInToClientTunnel = stdin.CopyToAsync(cryptoStream);
                    var clientToStdOutTunnel = cryptoStream.CopyToAsync(stdout);
                    await Task.WhenAll(stdInToClientTunnel, clientToStdOutTunnel);
                });
            }
        }
    }
}
