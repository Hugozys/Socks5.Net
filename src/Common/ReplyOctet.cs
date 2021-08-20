namespace Sock5.Net.Common
{
    internal enum ReplyOctet: byte
    {
        Succeed,
        GeneralFailure,
        ConnectionNotAllowed,
        NetworkUnreachable,
        HostUnreachable,
        ConnectionRefused,
        TTLExpired,
        CMDNotSupported,
        AddrTypeNotSupported,
    }
}
