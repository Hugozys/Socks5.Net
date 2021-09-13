using System.Net;
using System.Text;
using Socks5.Net.Common;

namespace Socks5.Net.Logging
{
    internal static class Extensions
    {
        public static EventState ToEventState(this RequestMessage message, ErrorCode? errorReason = null)
            => new(
                Constants.CommandTypeByteSet.Contains(message.CmdType) ? (CommandType)message.CmdType : CommandType.Unsupported,
                Constants.AddressTypeByteSet.Contains(message.AddrType) ? (AddressType)message.AddrType : AddressType.Unsupported,
                message.AddrType == (byte)AddressType.Domain ? Encoding.Default.GetString(message.Host) : new IPAddress(message.Host).ToString(),
                message.Port,
                errorReason);

        public static Microsoft.Extensions.Logging.LogLevel ToLogLevel(this LogLevel logLevel) => logLevel switch
        {
            LogLevel.Debug => Microsoft.Extensions.Logging.LogLevel.Debug,
            LogLevel.Info => Microsoft.Extensions.Logging.LogLevel.Information,
            LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
            LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
            LogLevel.Critical => Microsoft.Extensions.Logging.LogLevel.Critical
        };
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
}