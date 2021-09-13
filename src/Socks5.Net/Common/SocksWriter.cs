using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Socks5.Net.Logging;

namespace Socks5.Net.Common
{
    public class SocksWriter
    {
        private readonly PipeWriter _pipeWriter;
        private readonly ILogger<SocksWriter> _logger;

        public SocksWriter(PipeWriter writer)
        {
            _pipeWriter = writer ?? throw new ArgumentNullException(nameof(writer));
            _logger = Socks.LoggerFactory?.CreateLogger<SocksWriter>() ?? NoOpLogger<SocksWriter>.Instance; ;
        }

         /*
          *  +----+--------+
          *  |VER | METHOD |
          *  +----+--------+
          *  | 1  |   1    |
          *  +----+--------+
          */
        public async ValueTask<SocksResponse<byte>> SendSelectedAuthMethodAsync(ImmutableHashSet<byte> authSet, SocksOption sockOption, CancellationToken token = default)
        {
            var selected = (byte) AuthenticationMethod.NoAccept;
            foreach (var method in sockOption.SupportedAuthMethods)
            {
                var mbyte = (byte)method;
                if (authSet.Contains(mbyte))
                {
                    selected = mbyte;
                    break;
                }
            }
            Memory<byte> prepareBuffer = new byte[2] { Constants.Version, selected };
            var response = await SendReplyAsync(prepareBuffer, token);
            return response.ToGeneric(selected);
        }

        /*   +----+-----+-------+------+----------+----------+
         *   |VER | REP |  RSV  | ATYP | BND.ADDR | BND.PORT |
         *   +----+-----+-------+------+----------+----------+
         *   | 1  |  1  | X'00' |  1   | Variable |    2     |
         *   +----+-----+-------+------+----------+----------+
         */
        public ValueTask<SocksResponse> SendErrorReplyByErrorCodeAsync(ErrorCode code, CancellationToken token = default)
        {
            var replyByte = (byte)Utils.ErrorCodeToReplyOctet(code);
            var buffer = new byte[]{0x05, replyByte, 0x00, (byte)AddressType.IPV4, 0x00, 0x00, 0x00, 0x00, 0x01, 0x01};
            return SendReplyAsync(buffer, token);
        }
        
        public ValueTask<SocksResponse> SendSuccessReplyAsync(IPEndPoint? localEndpoint, CancellationToken token = default)
        {
           _ = localEndpoint ?? throw new ArgumentNullException(nameof(localEndpoint));

           var ip = localEndpoint.Address.GetAddressBytes();
           var addrType = ip.Length == 4 ? AddressType.IPV4 : AddressType.IPV6;

           var port = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(localEndpoint.Port))[^2..];

           var buffer = new List<byte[]>
           {
               new byte[]{0x05, 0x00, 0x00, (byte)addrType},
               ip, 
               port
           }.SelectMany(x => x).ToArray();
           return SendReplyAsync(buffer, token);
        }

        public async ValueTask<SocksResponse> SendReplyAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            var sendBuffer = _pipeWriter.GetMemory(buffer.Length);
            buffer.CopyTo(sendBuffer);
            _pipeWriter.Advance(buffer.Length);
            _logger.LogInformation("Sending Reply {Bytes}", buffer.ToArray());
            var flushResult = await _pipeWriter.FlushAsync(token);
  
            if (flushResult.IsCanceled)
            {
                return SocksResponseHelper.ErrorResult(ErrorCode.Cancelled);
            }
            return SocksResponse.SuccessResult;
        }
    }
}