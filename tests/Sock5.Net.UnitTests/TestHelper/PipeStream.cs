using System;
using System.IO;
using Sock5.Net.Pipe;

namespace Sock5.Net.UnitTests.TestHelper
{
    public static class PipeStream
    {
        public static SockPipe CreatePipeFromRStream(Memory<byte> payload, bool delayed = false)
        {
            Stream stream = delayed ? new DelayedMemoryStream(payload.ToArray()) : new MemoryStream(payload.ToArray());
            return new SockPipe(stream);
        }

        public static (SockPipe pipe, Stream stream) CreatePipeFromRWStream(bool delayed = false)
        {
            Stream stream = delayed ? new DelayedMemoryStream() : new MemoryStream();
            return (new SockPipe(stream), stream);
        }

    }
}