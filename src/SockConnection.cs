using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Sock5.Net.Auth;
using Sock5.Net.Command;
using Sock5.Net.Common;
using Sock5.Net.Pipe;
using Microsoft.Extensions.Logging;

namespace Sock5.Net
{
    public static class Sock
    {
        public static ILoggerFactory? LoggerFactory;
        public static SockConnection CreateSock(NetworkStream stream, SockOption? sockOption = null) => new(stream, sockOption ?? new SockOption());
    }

    public sealed class SockConnection : IDisposable
    {
        private readonly NetworkStream _stream;

        private readonly SockOption _sockOption;

        private readonly ILogger<SockConnection> _logger;

        public void Dispose()
        {
            _stream?.Dispose();
        }

        public SockConnection(NetworkStream stream, SockOption? sockOption = null)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _sockOption = sockOption ?? new SockOption();
            _logger = Sock.LoggerFactory?.CreateLogger<SockConnection>() ?? throw new ArgumentException("UnInitialized Sock.LoggerFactory");
        }

        public async Task ServeAsync()
        {

            using var pipe = new SockPipe(_stream);
            try
            {
                var authMResponse = await pipe.Reader.ReadAuthMethodsAsync();

                if (!authMResponse.Success)
                {
                    return;
                }
                var selectMResponse = await pipe.Writer.SendSelectedAuthMethodAsync(authMResponse.Payload!, _sockOption);

                if (!selectMResponse.Success || selectMResponse.Payload! == Constants.AuthMethods.NoAccept)
                {
                    _logger.LogDebug("No Acceptable Authentication Method");
                    return;
                }

                IAuthNegotiator authNegotiator = selectMResponse.Payload! switch
                {
                    Constants.AuthMethods.NoAuth => new NoAuthNegotiator(),
                    _ => throw new NotSupportedException()
                };

                var authResponse = await authNegotiator.NegotiateAsync(pipe);

                if (!authResponse.Success)
                {
                    _logger.LogDebug("Failed to perform authentication negotiation. {Reason}", authResponse.Reason);
                    return;
                }

                var sockResponse = await pipe.Reader.ReadRequestMessageAsync();

                if (!sockResponse.Success)
                {
                    _logger.LogDebug("Failed to read request payload. {Reason}", sockResponse.Reason);
                    await pipe.Writer.SendErrorReplyByErrorCodeAsync(sockResponse.Reason!.Value);
                    return;
                }

                var message = sockResponse.Payload;

                ICommandHandler handler = message.CmdType switch
                {
                    Constants.CMD.Connect => new ConnectCommandHandler(),
                    _ => new NotSupportedCommandHandler()
                };

                await handler.HandleAsync(pipe, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error ocurrred while handle sock connection");
            }
        }
    }
}