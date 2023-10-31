using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Socks5.Net.Common;
using Socks5.Net.Logging;
using Socks5.Net.Pipe;
using System.Text.Json;
using System.Threading;

namespace Socks5.Net.Command
{
    internal class ConnectCommandHandler : ICommandHandler
    {
        private readonly ILogger<ConnectCommandHandler> _logger;
        public ConnectCommandHandler()
        {
            _logger = Socks.LoggerFactory?.CreateLogger<ConnectCommandHandler>() ?? NoOpLogger<ConnectCommandHandler>.Instance;
        }
        public async Task HandleAsync(SocksPipe pipe, RequestMessage message, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Handling connection command...");
            var resolved = await Utils.ResolveIPAddressAsync(message.AddrType, message.Host);
            if (!resolved.Success)
            {
                _logger.LogError("Failed to resolve IP Address sent by client: {State}", JsonSerializer.Serialize(message.ToEventState(resolved.Reason)));
                await pipe.Writer.SendErrorReplyByErrorCodeAsync(resolved.Reason!.Value, message, cancellationToken);
                return;
            }

            var ip = resolved.Payload;
            _logger.LogDebug("Connecting to remote host...");
            var targetHostTcpClient = new TcpClient(ip!.ToString(), message.Port);
            var result = await pipe.Writer.SendSuccessReplyAsync((IPEndPoint?)targetHostTcpClient.Client.LocalEndPoint, cancellationToken);
            if (!result.Success)
            {
                _logger.LogError("Failed to send success reply: {State}", JsonSerializer.Serialize(message.ToEventState(result.Reason)));
                return;
            }
            var targetHostStream = targetHostTcpClient.GetStream();
            var clientStream = pipe.GetStream();

            _logger.LogDebug("Start tunneling...");
            var c2s = clientStream.CopyToAsync(targetHostStream, cancellationToken);
            var s2c = targetHostStream.CopyToAsync(clientStream, cancellationToken);
            await Task.WhenAll(c2s, s2c);
        }
    }
}