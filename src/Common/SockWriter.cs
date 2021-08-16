using System;
using System.Collections.Immutable;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Sock5.Net.Common
{
    public class SockWriter
    {
        private readonly PipeWriter _pipeWriter;

        public SockWriter(PipeWriter writer)
        {
            _pipeWriter = writer ?? throw new ArgumentNullException(nameof(writer));
        }

         /*
          *  +----+--------+
          *  |VER | METHOD |
          *  +----+--------+
          *  | 1  |   1    |
          *  +----+--------+
          */
        public async ValueTask<SockResponse<byte>> SendSelectedAuthMethodAsync(ImmutableHashSet<byte> authSet, SockOption sockOption, CancellationToken token = default)
        {
            byte selected = Constants.AuthMethods.NoAccept;
            foreach (var method in sockOption.SupportedAuthMethods)
            {
                if (authSet.Contains(method))
                {
                    selected = method;
                    break;
                }
            }
            Memory<byte> prepareBuffer = new byte[2] { Constants.Version, selected };
            var response = await SendReplyAsync(prepareBuffer, token);
            return response.ToGeneric(selected);
        }

        // public async ValueTask<SockResponse> SendReplyByRequestMessage(SockResponse<RequestMessage> request, CancellationToken token = default)
        // {

        // }


        /*   +----+-----+-------+------+----------+----------+
         *   |VER | REP |  RSV  | ATYP | BND.ADDR | BND.PORT |
         *   +----+-----+-------+------+----------+----------+
         *   | 1  |  1  | X'00' |  1   | Variable |    2     |
         *   +----+-----+-------+------+----------+----------+
         */
        public ValueTask<SockResponse> SendErrorReplyByErrorCodeAsync(ErrorCode code, CancellationToken token = default)
        {
            var replyByte = (byte)Utils.ErrorCodeToReplyOctet(code);
            var buffer = new byte[]{0x05, replyByte, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x01, 0x01};
            return SendReplyAsync(buffer, token);
        }
        
        public ValueTask<SockResponse> SendSuccessReplyAsync(IPEndPoint? localEndpoint, CancellationToken token = default)
        {
           _ = localEndpoint ?? throw new ArgumentNullException(nameof(localEndpoint));
           var ip = localEndpoint.Address.GetAddressBytes();
           var port = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(localEndpoint.Port))[^2..];
           var buffer = new byte[]{0x05, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x01, 0x01};
           return SendReplyAsync(buffer, token);
        }

        public async ValueTask<SockResponse> SendReplyAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            var sendBuffer = _pipeWriter.GetMemory(buffer.Length);
            buffer.CopyTo(sendBuffer);
            _pipeWriter.Advance(buffer.Length);
            var flushResult = await _pipeWriter.FlushAsync(token);
  
            if (flushResult.IsCanceled)
            {
                return SockResponseHelper.ErrorResult(ErrorCode.Cancelled);
            }
            return SockResponse.SuccessResult;
        }
    }
}