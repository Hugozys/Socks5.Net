using System.Threading.Tasks;
using Socks5.Net.Common;
using Socks5.Net.Pipe;

namespace Socks5.Net.Command
{
    public interface ICommandHandler
    {
        Task HandleAsync(SocksPipe pipe, RequestMessage message);
    }
}