namespace Sock5.Net
{
    public enum ReplyOctet: byte
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
