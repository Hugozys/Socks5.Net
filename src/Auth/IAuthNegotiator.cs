using System.Threading.Tasks;
using Sock5.Net.Common;
using Sock5.Net.Pipe;

namespace Sock5.Net
{
    public interface IAuthNegotiator
    {
        Task<SockResponse> NegotiateAsync(SockPipe sockPipe);
    }
}