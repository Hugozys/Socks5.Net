using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Socks5.Net.Common
{
    internal static class Utils
    {
        public static async Task<SocksResponse<IPAddress>> ResolveIPAddressAsync(byte addrType, byte[] address)
        {
            IPAddress ip;
            if (addrType == (byte)AddressType.Domain)
            {
                var hostname = Encoding.Default.GetString(address);
                var hostNameType = Uri.CheckHostName(hostname);
                if (hostNameType == UriHostNameType.Unknown)
                {
                    return SocksResponseHelper.ErrorResult<IPAddress>(ErrorCode.InvalidHostName);
                }
                try
                {
                    ip = (await Dns.GetHostAddressesAsync(hostname))[0];
                }
                catch(Exception)
                {
                    return SocksResponseHelper.ErrorResult<IPAddress>(ErrorCode.UnreachableHost);
                }
            }
            else
            {
                ip = new IPAddress(address);
            }
            return SocksResponseHelper.SuccessResult(ip);
        }

        internal static ReplyOctet ErrorCodeToReplyOctet(ErrorCode code) => code switch
        {
            ErrorCode.InvalidCmd or ErrorCode.UnsupportedCmd => ReplyOctet.CMDNotSupported,
            ErrorCode.UnreachableHost => ReplyOctet.HostUnreachable,
            _ => ReplyOctet.GeneralFailure
        };
    }
}