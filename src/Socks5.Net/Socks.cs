using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Socks5.Net.Logging;

namespace Socks5.Net
{
    public static class Socks
    {
        private static ILoggerFactory? _loggerFactory;
        public static ILoggerFactory? LoggerFactory { get => _loggerFactory; }

        public static void SetLogLevel(Logging.LogLevel level = Logging.LogLevel.Debug)
        {
            if (_loggerFactory is not null)
            {
                throw new Exception("SetLogLevel can only be called once.");
            }

            _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(
                builder => builder
                            .AddConsole()
                            .SetMinimumLevel(level.ToLogLevel()));
        }
        public static SocksConnection CreateSock(Stream stream, SocksOption? sockOption = null) => new(stream, sockOption ?? new SocksOption());
    }
}