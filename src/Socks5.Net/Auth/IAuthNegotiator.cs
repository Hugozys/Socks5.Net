using System.Threading.Tasks;
using Socks5.Net.Common;
using Socks5.Net.Pipe;

namespace Socks5.Net
{
    public interface IAuthNegotiator
    {
        Task<SocksResponse> NegotiateAsync(SocksPipe sockPipe);
    }
}