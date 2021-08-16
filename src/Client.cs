using System;
using System.Net.Sockets;

namespace Sock5.Net
{
    public class Client : IDisposable
    {
        private readonly TcpClient _tcpClient;

        public Client()
        {

        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}