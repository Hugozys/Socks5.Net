using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Socks5.Net.Common;
using Socks5.Net.Logging;
using Socks5.Net.Pipe;

namespace Socks5.Net.Auth
{
    internal class NoAuthNegotiator: IAuthNegotiator
    {
        private readonly ILogger<NoAuthNegotiator> _logger;
        public NoAuthNegotiator()
        {
            _logger = Socks.LoggerFactory?.CreateLogger<NoAuthNegotiator>() ?? NoOpLogger<NoAuthNegotiator>.Instance;
        }

        public Task<SocksResponse> NegotiateAsync(SocksPipe sockPipe)
        {
            _logger.LogDebug("Performing no auth negotiation");
            return Task.FromResult(SocksResponse.SuccessResult);
        }
    }
}