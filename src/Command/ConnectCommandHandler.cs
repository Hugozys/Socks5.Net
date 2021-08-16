using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Sock5.Net.Common;
using Sock5.Net.Pipe;

namespace Sock5.Net.Command
{
    public class ConnectCommandHandler : ICommandHandler
    {
        public async Task HandleAsync(SockPipe pipe, byte addressType, byte[] address, int port)
        {
            var resolved = await Utils.ResolveIPAddressAsync(addressType, address);
            if (!resolved.Success)
            {
                await pipe.Writer.SendErrorReplyByErrorCodeAsync(resolved.Reason!.Value);
                return;
            }
            // todo: implement connect
            var ip = resolved.Payload;
            var targetHostTcpClient = new TcpClient(ip!.ToString(), port);
            await pipe.Writer.SendSuccessReplyAsync((IPEndPoint?) targetHostTcpClient.Client.LocalEndPoint);
            var targetHostStream = targetHostTcpClient.GetStream();
            var clientStream = pipe.GetStream();
            var c2s = clientStream.CopyToAsync(targetHostStream);
            var s2c = targetHostStream.CopyToAsync(clientStream);
            await Task.WhenAll(c2s, s2c);
        }
    }
}