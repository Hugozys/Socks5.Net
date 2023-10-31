using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Socks5.Net.Common;
using Socks5.Net.Logging;
using Socks5.Net.Pipe;

namespace Socks5.Net.Command
{
    internal class NotSupportedCommandHandler : ICommandHandler
    {
        private readonly ILogger<NotSupportedCommandHandler> _logger;

        public NotSupportedCommandHandler()
        {
            _logger = Socks.LoggerFactory?.CreateLogger<NotSupportedCommandHandler>() ?? NoOpLogger<NotSupportedCommandHandler>.Instance;
        }
        public async Task HandleAsync(SocksPipe pipe, RequestMessage message, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Command not supported. {State}", message.ToEventState());
            await pipe.Writer.SendErrorReplyByErrorCodeAsync(ErrorCode.UnsupportedCmd, message, cancellationToken);
            return;
        }
    }
}