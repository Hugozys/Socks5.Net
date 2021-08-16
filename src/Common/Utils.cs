using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Sock5.Net.Common
{
    public static class Utils
    {
        public static async Task<SockResponse<IPAddress>> ResolveIPAddressAsync(byte addrType, byte[] address)
        {
            IPAddress ip;
            if (addrType == Constants.AddrType.Domain)
            {
                var hostname = Encoding.Default.GetString(address);
                var hostNameType = Uri.CheckHostName(hostname);
                if (hostNameType == UriHostNameType.Unknown)
                {
                    return SockResponseHelper.ErrorResult<IPAddress>(ErrorCode.InvalidHostName);
                }
                try
                {
                    ip = (await Dns.GetHostAddressesAsync(hostname))[0];
                }
                catch(Exception)
                {
                    return SockResponseHelper.ErrorResult<IPAddress>(ErrorCode.UnreachableHost);
                }
            }
            else
            {
                ip = new IPAddress(address);
            }
            return SockResponseHelper.SuccessResult(ip);
        }

        public static ReplyOctet ErrorCodeToReplyOctet(ErrorCode code) => code switch
        {
            ErrorCode.InvalidCmd => ReplyOctet.CMDNotSupported,
            ErrorCode.UnreachableHost => ReplyOctet.HostUnreachable,
            _ => ReplyOctet.GeneralFailure
        };
    }
}