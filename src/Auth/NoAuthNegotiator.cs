using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sock5.Net.Common;
using Sock5.Net.Pipe;

namespace Sock5.Net.Auth
{
    internal class NoAuthNegotiator: IAuthNegotiator
    {
        private readonly ILogger<NoAuthNegotiator> _logger;
        public NoAuthNegotiator()
        {
            _logger = Sock.LoggerFactory?.CreateLogger<NoAuthNegotiator>() ?? throw new ArgumentException("UnInitialized Sock.LoggerFactory");
        }

        public Task<SockResponse> NegotiateAsync(SockPipe sockPipe)
        {
            _logger.LogDebug("Performing no auth negotiation");
            return Task.FromResult(SockResponse.SuccessResult);
        }
    }
}