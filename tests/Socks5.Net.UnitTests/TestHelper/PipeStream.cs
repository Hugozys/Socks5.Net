using System;
using System.IO;
using Socks5.Net.Pipe;

namespace Socks5.Net.UnitTests.TestHelper
{
    public static class PipeStream
    {
        public static SocksPipe CreatePipeFromRStream(Memory<byte> payload, bool delayed = false)
        {
            Stream stream = delayed ? new DelayedMemoryStream(payload.ToArray()) : new MemoryStream(payload.ToArray());
            return new SocksPipe(stream);
        }

        public static (SocksPipe pipe, Stream stream) CreatePipeFromRWStream(bool delayed = false)
        {
            Stream stream = delayed ? new DelayedMemoryStream() : new MemoryStream();
            return (new SocksPipe(stream), stream);
        }

    }
}