using System;
using System.Threading.Tasks;
using Socks5.Net.Auth;
using Socks5.Net.Command;
using Socks5.Net.Pipe;
using Microsoft.Extensions.Logging;
using System.IO;
using Socks5.Net.Logging;
using System.Net;
using Socks5.Net.Common;
using System.Threading;
using System.ComponentModel;

namespace Socks5.Net
{
    public sealed class SocksConnection : IDisposable
    {
        private readonly Stream _stream;

        private readonly EndPoint _remoteEndPoint;

        private readonly SocksOption _sockOption;

        private readonly ILogger<SocksConnection> _logger;

        public void Dispose()
        {
            _stream?.Dispose();
        }

        internal SocksConnection(Stream stream, EndPoint remoteEndpoint, SocksOption? sockOption = default)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _remoteEndPoint = remoteEndpoint;
            _sockOption = sockOption ?? new SocksOption();
            _logger = Socks.LoggerFactory?.CreateLogger<SocksConnection>() ?? NoOpLogger<SocksConnection>.Instance;
        }

        public async Task ServeAsync(CancellationToken cancellationToken = default)
        {

            using var pipe = new SocksPipe(_stream);
            try
            {
                _logger.LogDebug("Reading Sock v5 Authentication Methods...");
                var authMResponse = await pipe.Reader.ReadAuthMethodsAsync(cancellationToken);

                if (!authMResponse.Success)
                {
                    return;
                }
                var selectMResponse = await pipe.Writer.SendSelectedAuthMethodAsync(authMResponse.Payload!, _sockOption, cancellationToken);
                if (!selectMResponse.Success || selectMResponse.Payload! == (byte)AuthenticationMethod.NoAccept)
                {
                    _logger.LogError("No Acceptable Authentication Method");
                    return;
                }

                IAuthNegotiator authNegotiator = selectMResponse.Payload! switch
                {
                    (byte)AuthenticationMethod.NoAuth => new NoAuthNegotiator(),
                    _ => throw new NotSupportedException()
                };

                var authResponse = await authNegotiator.NegotiateAsync(pipe);

                if (!authResponse.Success)
                {
                    _logger.LogError("Failed to perform authentication negotiation. {Reason}", authResponse.Reason);
                    return;
                }

                _logger.LogDebug("Reading Sock v5 Request...");
                var sockResponse = await pipe.Reader.ReadRequestMessageAsync(cancellationToken);

                if (!sockResponse.Success)
                {
                    _logger.LogError("Failed to read request payload. {Reason}", sockResponse.Reason);
                    await pipe.Writer.SendErrorReplyByErrorCodeAsync(sockResponse.Reason!.Value, DummyRequestMessage.Instance, cancellationToken);
                    return;
                }

                var message = sockResponse.Payload;

                ICommandHandler handler = message.CmdType switch
                {
                    (byte)CommandType.Connect => new ConnectCommandHandler(),
                    (byte)CommandType.UDP => new UDPAssociateCommandHandler(_remoteEndPoint, _sockOption.UDPRelayAddr),
                    _ => new NotSupportedCommandHandler()
                };
                _logger.LogDebug("Handling Sock v5 Commands...");
                await handler.HandleAsync(pipe, message, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error ocurrred while handle sock connection");
            }
        }
    }
}