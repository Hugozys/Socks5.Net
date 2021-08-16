using System;
using System.Threading.Tasks;
using Sock5.Net.Common;
using Sock5.Net.Pipe;

namespace Sock5.Net.Auth
{
    public class NoAuthNegotiator: IAuthNegotiator
    {
        public NoAuthNegotiator()
        {
        }

        public Task<SockResponse> NegotiateAsync(SockPipe sockPipe) => Task.FromResult(SockResponse.SuccessResult);
    }
}