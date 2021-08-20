using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sock5.Net.Common;
using Sock5.Net.Logging;
using Sock5.Net.Pipe;

namespace Sock5.Net.Command
{
    internal class ConnectCommandHandler : ICommandHandler
    {
        private readonly ILogger<ConnectCommandHandler> _logger;
        public ConnectCommandHandler()
        {
            _logger = Sock.LoggerFactory?.CreateLogger<ConnectCommandHandler>() ?? throw new ArgumentException("UnInitialized Sock.LoggerFactory");
        }
        public async Task HandleAsync(SockPipe pipe, RequestMessage message)
        {
            _logger.LogDebug("Handling connection command...");
            var resolved = await Utils.ResolveIPAddressAsync(message.AddrType, message.Host);
            if (!resolved.Success)
            {
                _logger.LogError("Failed to resolve IP Address sent by client: {State}", message.ToEventState(resolved.Reason));
                await pipe.Writer.SendErrorReplyByErrorCodeAsync(resolved.Reason!.Value);
                return;
            }

            var ip = resolved.Payload;
            _logger.LogDebug("Connecting to remote host...");
            var targetHostTcpClient = new TcpClient(ip!.ToString(), message.Port);
            var result = await pipe.Writer.SendSuccessReplyAsync((IPEndPoint?) targetHostTcpClient.Client.LocalEndPoint);
            if (!result.Success)
            {
                _logger.LogError("Failed to send success reply: {State}", message.ToEventState(result.Reason));
                return;
            }
            var targetHostStream = targetHostTcpClient.GetStream();
            var clientStream = pipe.GetStream();

            _logger.LogDebug("Start tunneling...");
            var c2s = clientStream.CopyToAsync(targetHostStream);
            var s2c = targetHostStream.CopyToAsync(clientStream);

            await Task.WhenAll(c2s, s2c);
        }
    }
}