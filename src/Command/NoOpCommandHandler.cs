using System.Net.Sockets;
using System.Threading.Tasks;
using Sock5.Net.Pipe;

namespace Sock5.Net.Command
{
    public class NoOpCommandHandler : ICommandHandler
    {
        public Task HandleAsync(SockPipe pipe, byte addressType, byte[] address, int port)
        {
            throw new System.NotImplementedException();
        }
    }
}