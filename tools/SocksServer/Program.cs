using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Socks5.Net;
using Socks5.Net.Security;
using System;
using Socks5.Net.Logging;
using System.CommandLine;
using System.CommandLine.Invocation;
using Socks5.Net.Extensions.Security;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace SocksServer
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var rootCommand = new RootCommand
            {
                new Option<Mode>(new[] { "--mode", "-m" }, () => Mode.PlainText, "crypto mode to operate on"),
                new Option<int>(new[] { "--port", "-p" }, () => 8080, "port on which the socks server will listen for traffic"),
                new Option<bool>(new[] { "--verbose", "-v" }, () => false, "whether to use verbose logging"),
                new Option<IPAddress>(
                    aliases: new[] {"--udp-relay", "-u"},
                    parseArgument: result => {
                        if (result.Tokens.Count == 0){
                            result.ErrorMessage = "missing --udp-relay(-u) option";
                            return null;
                        }
                        string ip = result.Tokens.Single().Value;
                        if (!IPAddress.TryParse(ip, out IPAddress parsedIP)){
                            result.ErrorMessage = $"{ip} is not a valid ip address.";
                            return null;
                        }
                        return parsedIP;
                    },
                    isDefault: true,
                    "ip address used to receive udp packet from remote host/client"){IsRequired = true, }
            };
            rootCommand.Handler = CommandHandler.Create<Mode, int, bool, IPAddress>(Start);
            await rootCommand.InvokeAsync(args);
        }

        public static async Task Start(Mode mode, int port, bool verbose, IPAddress udpRelay)
        {
            Socks.SetLogLevel(verbose ? Socks5.Net.Logging.LogLevel.Debug : Socks5.Net.Logging.LogLevel.Error);
            var mainLogger = Socks.LoggerFactory?.CreateLogger("Main") ?? NoOpLogger.Instance;
            var listener = new TcpListener(IPAddress.Any, port);
            mainLogger.LogInformation("Listening on port {port}...", port);
            listener.Start();
            while (true)
            {
                var client = await listener.AcceptTcpClientAsync();
                mainLogger.LogInformation("Received connection from client");
                _ = Task.Run(async () =>
                {
                    var networkStream = client.GetStream();
                    var cryptoStream = await Crypto.GetServerStreamAsync(networkStream, mode);
                    using var sockConnect = Socks.CreateSock(
                        cryptoStream,
                        networkStream.Socket.RemoteEndPoint!,
                        new SocksOption { UDPRelayAddr = udpRelay });
                    await sockConnect.ServeAsync();
                });
            }
        }
    }
}
