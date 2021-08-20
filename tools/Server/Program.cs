using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Sock5.Net;

namespace SockServer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 1080);
            listener.Start();
            while (true)
            {
                Console.WriteLine("Start Accepting client connections...");
                var client = await listener.AcceptTcpClientAsync();
                _ = Task.Run(async () =>
                  {
                      using var sockConnect = new SockConnection(client.GetStream());
                      await sockConnect.ServeAsync();
                  });
            }
        }
    }
}
