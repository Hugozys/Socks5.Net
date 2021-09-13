namespace Socks5.Net
{
    public enum AuthenticationMethod: byte
    {
         NoAuth = 0x00,
         GSSAPI = 0x01,
         UserNameAndPassword = 0x02,
         NoAccept = 0xFF
    }

    public enum AddressType: byte
    {
        IPV4 = 0x01,
        Domain = 0x03,
        IPV6 = 0x04,
        Unsupported = 0x05
    }

    public enum CommandType: byte
    {
        Connect = 0x01,
        Bind = 0x02,
        UDP = 0x03,
        Unsupported = 0x04
    }
}
