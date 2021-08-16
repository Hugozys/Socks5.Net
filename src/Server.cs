using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Sock5.Net.Auth;
using Sock5.Net.Command;
using Sock5.Net.Pipe;

namespace Sock5.Net
{
    public sealed class Server
    {
        private readonly TcpListener _listener;

        private readonly SockOption _sockOption;

        private readonly static IPAddress LocalIP = IPAddress.Parse("127.0.0.1");

        public Server(int port, SockOption? sockOption = null)
        {
            _listener = new TcpListener(LocalIP, port);
            _sockOption = sockOption ?? new SockOption();
        }

        public async Task StartAsync()
        {
            _listener.Start();
            while (true)
            {
                Console.WriteLine("Start Accepting client connections...");
                var client = await _listener.AcceptTcpClientAsync();
                Task.Run(async () => await ServeAsync(client, _sockOption));
            }
        }

        public async Task ServeAsync(TcpClient client, SockOption sockOption)
        {
            using var pipe = new SockPipe(client.GetStream());

            var authMResponse = await pipe.Reader.ReadAuthMethodsAsync();

            if (!authMResponse.Success)
            {
                return;
            }
            var selectMResponse = await pipe.Writer.SendSelectedAuthMethodAsync(authMResponse.Payload!, sockOption);

            if (!selectMResponse.Success || selectMResponse.Payload! == Constants.AuthMethods.NoAccept)
            {
                return;
            }

            var authNegotiator = selectMResponse.Payload! switch
            {
                Constants.AuthMethods.NoAuth => new NoAuthNegotiator(),
                _ => throw new NotSupportedException()
            };

            var authResponse = await authNegotiator.NegotiateAsync(pipe);

            if (!authResponse.Success)
            {
                return;
            }

            var sockResponse = await pipe.Reader.ReadRequestMessageAsync();

            // if (!sockResponse.Success)
            // {
            //     await pipe.Writer.SendErrorReplyByErrorCode(sockResponse.Reason!.Value);
            //     return;
            // }
            // var message = sockResponse.Payload;

            // ICommandHandler handler = message.CmdType switch
            // {
            //     Constants.CMD.Connect => new ConnectCommandHandler(),
            //     _ => new NoOpCommandHandler()
            // };

            //await handler.HandleAsync(pipe);
        }
    }
}