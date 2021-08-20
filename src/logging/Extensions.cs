using System.Net;
using System.Text;
using Sock5.Net.Common;

namespace Sock5.Net.Logging
{
    internal static class Extensions
    {
        public static EventState ToEventState(this RequestMessage message, ErrorCode? errorReason = null) 
            => new(
                Constants.CMD.CmdSet.Contains(message.CmdType) ? (CommandType) message.CmdType : CommandType.Unsupported,
                Constants.AddrType.AddrTypeSet.Contains(message.AddrType) ? (AddressType) message.AddrType: AddressType.Unsupported,
                message.AddrType == Constants.AddrType.Domain ? Encoding.Default.GetString(message.Host) : new IPAddress(message.Host).ToString(),
                message.Port,
                errorReason);

    }

    internal struct EventState
    {
        public CommandType CmdType { get; set; }
        public AddressType AddressType { get; set; }

        public string Address { get; set; }

        public int Port { get; set; }

        public ErrorCode? ErrorReason { get; set; }

        public EventState(CommandType cmdType, AddressType addrType, string address, int port, ErrorCode? errorCode)
        {
            CmdType = cmdType;
            AddressType = addrType;
            Address = address;
            Port = port;
            ErrorReason = errorCode;
        }
    }

    internal enum AddressType: byte
    {
        IPV4 = 0x01,
        DomainName = 0x03,
        IPV6 = 0x04,
        Unsupported = 0x05
    }

    internal enum CommandType: byte
    {
        Connect = 0x01,
        Bind = 0x02,
        UDP = 0x03,
        Unsupported = 0x04
    }
}