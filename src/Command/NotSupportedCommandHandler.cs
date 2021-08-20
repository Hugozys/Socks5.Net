using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sock5.Net.Common;
using Sock5.Net.Logging;
using Sock5.Net.Pipe;

namespace Sock5.Net.Command
{
    internal class NotSupportedCommandHandler : ICommandHandler
    {
        private readonly ILogger<NotSupportedCommandHandler> _logger;

        public NotSupportedCommandHandler()
        {
            _logger = Sock.LoggerFactory?.CreateLogger<NotSupportedCommandHandler>() ?? throw new ArgumentException("UnInitialized Sock.LoggerFactory");
        }
        public async Task HandleAsync(SockPipe pipe, RequestMessage message)
        {
            _logger.LogDebug("Command not supported. {State}", message.ToEventState());
            await pipe.Writer.SendErrorReplyByErrorCodeAsync(ErrorCode.UnsupportedCmd);
            return;
        }
    }
}