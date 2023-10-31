using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Socks5.Net.Common;
using Socks5.Net.Logging;
using Socks5.Net.Pipe;
using System.Text.Json;
using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Globalization;

namespace Socks5.Net.Command
{
    public readonly struct UDPDatagram
    {
        public byte[] Rsv { get; init; } // 0x00 0x00
        public byte Frag { get; init; }
        public byte AddrType { get; init; }
        public byte[] DstAddr { get; init; }
        public byte[] DstPort { get; init; } // 2 bytes
        public int Port { get; init; }
        public byte[] Data { get; init; }

        private UDPDatagram(
            Span<byte> rsv,
            byte frag,
            byte adrTyp,
            Span<byte> dstAddr,
            Span<byte> dstPort,
            Span<byte> data)
        {
            Rsv = rsv.ToArray();
            Frag = frag;
            AddrType = adrTyp;
            DstAddr = dstAddr.ToArray();
            DstPort = dstPort.ToArray();
            Port = (ushort)IPAddress.NetworkToHostOrder(BitConverter.ToInt16(DstPort));
            Data = data.ToArray();
        }

        // +----+------+------+----------+----------+----------+
        // |RSV | FRAG | ATYP | DST.ADDR | DST.PORT |   DATA   |
        // +----+------+------+----------+----------+----------+
        // | 2  |  1   |  1   | Variable |    2     | Variable |
        // +----+------+------+----------+----------+----------+
        //
        //  The fields in the UDP request header are:
        //  o RSV  Reserved X'0000'
        //  o FRAG    Current fragment number
        //  o ATYP address type of following addresses:
        //  o IP V4 address: X'01'
        //  o DOMAINNAME: X'03'
        //  o IP V6 address: X'04'
        //  o DST.ADDR       desired destination address
        //  o DST.PORT desired destination port
        //  o DATA     user data
        public static SocksResponse<UDPDatagram> Parse(Span<byte> buffer)
        {
            if (buffer.Length < Constants.UDPHeaderMinLen)
            {
                return SocksResponseHelper.ErrorResult<UDPDatagram>(ErrorCode.InComplete);
            }
            if (buffer[0] != Constants.Rsv || buffer[1] != Constants.Rsv)
            {
                return SocksResponseHelper.ErrorResult<UDPDatagram>(ErrorCode.InvalidRsv);
            }
            // Implementation of fragmentation is optional; an implementation that
            // does not support fragmentation MUST drop any datagram whose FRAG
            // field is other than X'00'.
            if (buffer[2] != Constants.Frag)
            {
                return SocksResponseHelper.ErrorResult<UDPDatagram>(ErrorCode.NotAllowedByRuleSet);
            }
            var addrPtr = buffer[3..];

            (int addrLen, int skip) = addrPtr[0] switch
            {
                (byte)AddressType.IPV4 => (4, 1),
                (byte)AddressType.IPV6 => (16, 1),
                (byte)AddressType.Domain => (addrPtr[1], 2),
                _ => (-1, -1)
            };
            if (addrLen == -1)
            {
                return SocksResponseHelper.ErrorResult<UDPDatagram>(ErrorCode.InvalidAddrType);
            }
            // UDPHeaderMinLen includes the possibile additional byte that indicates domain length
            // when address type is domain, so we need to exclude one byte 
            // when calculating the min length if the address type is ip
            int offset = addrPtr[0] == (byte)AddressType.Domain ? addrLen : addrLen - 1;
            if (buffer.Length < (Constants.UDPHeaderMinLen + offset))
            {
                return SocksResponseHelper.ErrorResult<UDPDatagram>(ErrorCode.InComplete);
            }
            addrPtr = addrPtr[skip..];
            var portPtr = addrPtr[addrLen..];
            var dataPtr = portPtr[2..];
            return SocksResponseHelper.SuccessResult(new UDPDatagram(
                buffer[..2],
                buffer[2],
                buffer[3],
                addrPtr[..addrLen],
                portPtr[..2],
                dataPtr
            ));
        }
        public byte[] ToBytes()
        {
            var list = new List<byte[]> { Rsv, new byte[] { Frag, AddrType } };
            if (AddrType == (byte)AddressType.Domain)
            {
                list.Add(new byte[] { (byte)DstAddr.Length });
            }
            list.Add(DstAddr);
            list.Add(DstPort);
            list.Add(Data);
            return list.SelectMany(x => x).ToArray();
        }

        public UDPDatagram CopyHeader(Span<byte> buffer)
        {
            return new UDPDatagram(Rsv, Frag, AddrType, DstAddr, DstPort, buffer);
        }
    }

    internal class UDPAssociateCommandHandler : ICommandHandler
    {
        private readonly ILogger<UDPAssociateCommandHandler> _logger;
        private readonly IPEndPoint _remoteEndPoint;

        private readonly IPAddress _udpRelayAddr;
        private readonly Dictionary<string, UdpClient> _cache;
        private async ValueTask<SocksResponse<IPEndPoint>> GetClientAddressAsync(RequestMessage message)
        {
            // If the requested Host/Port is all zeros, the relay should simply use the Host/Port 
            // that sent the request.
            // https://stackoverflow.com/questions/62283351/how-to-use-socks-5-proxy-with-tidudpclient-properly
            if (message.Port == 0 && message.Host.All(b => b == 0))
            {
                return SocksResponseHelper.SuccessResult(_remoteEndPoint);
            }
            if (message.Port == 0 || message.Host.All(b => b == 0))
            {
                return SocksResponseHelper.ErrorResult<IPEndPoint>(ErrorCode.NotAllowedByRuleSet);
            }
            var resolved = await Utils.ResolveIPAddressAsync(message.AddrType, message.Host);
            if (!resolved.Success)
            {
                _logger.LogError("Failed to resolve IP Address sent by client: {State}", JsonSerializer.Serialize(message.ToEventState(resolved.Reason)));
                return resolved.ToGeneric<IPAddress, IPEndPoint>();
            }
            return SocksResponseHelper.SuccessResult(new IPEndPoint(resolved.Payload!, message.Port));
        }

        public UDPAssociateCommandHandler(EndPoint remoteEndPoint, IPAddress udpRelayAddr)
        {
            _remoteEndPoint = remoteEndPoint as IPEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
            _udpRelayAddr = udpRelayAddr;
            _logger = Socks.LoggerFactory?.CreateLogger<UDPAssociateCommandHandler>() ?? NoOpLogger<UDPAssociateCommandHandler>.Instance;
            _cache = new Dictionary<string, UdpClient>();
        }
        public async Task HandleAsync(SocksPipe pipe, RequestMessage message, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Handling udp associate command...");

            var epResponse = await GetClientAddressAsync(message);
            if (!epResponse.Success)
            {
                await pipe.Writer.SendErrorReplyByErrorCodeAsync(epResponse.Reason!.Value, message, cancellationToken);
                return;
            }

            using var clientRelay = new UdpClient(new IPEndPoint(_udpRelayAddr, 0));
            // If the socks server is running on the cloud, it is very likely
            // that the local endpoint is bounded to a private IP. We need to add
            // flag to allow users to explicitly specify public IP address where
            // the client should send the UDP relay to when socks server sends 
            // reply to a UDP ASSOCIATE request.
            var socksServerAddress = clientRelay.Client.LocalEndPoint as IPEndPoint;
            // fields indicate the port number/ address where the client MUST send
            // UDP request messages to be relayed.
            _logger.LogDebug("Client relay binds to local endpoint: {endpoint}", socksServerAddress!.ToString());
            await pipe.Writer.SendSuccessReplyAsync(socksServerAddress, cancellationToken);

            var expectClientAddress = epResponse.Payload!;
            _ = Task.Run(async () =>
            {
                try
                {
                    await RelayClientDatagramAsync(clientRelay, expectClientAddress, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error happened while relaying data from client to server. Message: {message}, Stacktrace: {trace}", ex.Message, ex.StackTrace);
                }
            });

            var reader = pipe.Reader.GetPipeReader();
            while (true)
            {
                var readResult = await reader.ReadAsync(cancellationToken);
                var buffer = readResult.Buffer;
                if (readResult.IsCompleted)
                {
                    clientRelay.Close();
                    return;
                }
                reader.AdvanceTo(buffer.End);
            }
        }

        private async Task RelayClientDatagramAsync(UdpClient clientRelay, IPEndPoint clientAddress, CancellationToken cancellationToken = default)
        {
            while (true)
            {
                var result = await clientRelay.ReceiveAsync(cancellationToken);
                var actualClientAddress = result.RemoteEndPoint!;
                if (!actualClientAddress.Equals(clientAddress))
                {
                    _logger.LogDebug(
                        "Expect UDP address sent from {expectClientAddress}, but was from {actualClientAddress}",
                        clientAddress,
                        actualClientAddress);
                    continue;
                }
                var datagramResult = UDPDatagram.Parse(result.Buffer);
                if (!datagramResult.Success)
                {
                    _logger.LogError("Failed to parse UDP datagram: {code}", datagramResult.Reason!);
                    continue;
                }
                var datagram = datagramResult.Payload;
                var resolve = await Utils.ResolveIPAddressAsync(datagram.AddrType, datagram.DstAddr);
                if (!resolve.Success)
                {
                    _logger.LogError("Failed to resolve host name in UDP header: {code}", resolve.Reason);
                    continue;
                }
                var dstAddress = resolve.Payload!;
                var remoteEndpoint = new IPEndPoint(dstAddress, datagram.Port);
                var clientKey = remoteEndpoint.ToString();
                if (!_cache.ContainsKey(clientKey))
                {
                    var newRemoteConn = new UdpClient(new IPEndPoint(_udpRelayAddr, 0));
                    newRemoteConn.Connect(remoteEndpoint);
                    _cache[clientKey] = newRemoteConn;
                    _ = Task.Run(async () =>
                        {
                            try
                            {
                                while (true)
                                {
                                    var result = await newRemoteConn.ReceiveAsync(cancellationToken);
                                    var backDatagram = datagram.CopyHeader(result.Buffer);
                                    await clientRelay.SendAsync(backDatagram.ToBytes().AsMemory(), clientAddress, cancellationToken);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError("Error happened while relaying data from server to client. Message: {message}, StackTrace: {trace}", ex.Message, ex.StackTrace);
                            }
                        });
                }
                var remoteConn = _cache[clientKey];
                await remoteConn.SendAsync(datagram.Data.AsMemory(), cancellationToken);
            }
        }
    }
}