using System.Threading.Tasks;
using Sock5.Net.Common;
using Sock5.Net.Pipe;

namespace Sock5.Net.Command
{
    public interface ICommandHandler
    {
        Task HandleAsync(SockPipe pipe, RequestMessage message);
    }
}