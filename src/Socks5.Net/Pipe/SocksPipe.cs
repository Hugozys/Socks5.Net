using System;
using System.IO;
using System.IO.Pipelines;
using System.Net.Sockets;
using Socks5.Net.Common;

namespace Socks5.Net.Pipe
{
    public sealed class SocksPipe: IDisposable
    {
        private readonly Stream _stream;

        private readonly SocksReader _reader;

        private readonly SocksWriter _writer;

        public SocksReader Reader => _reader;

        public SocksWriter Writer => _writer;

        public SocksPipe(Stream stream, StreamPipeReaderOptions? readerOptions = null, StreamPipeWriterOptions? writerOptions = null)
        {
            if (stream is null || !stream.CanRead || !stream.CanWrite)
            {
                throw new ArgumentException($"{nameof(stream)} must be readable and writable network stream");
            }
            _stream = stream;
            _reader = new SocksReader(PipeReader.Create(_stream, readerOptions));
            _writer = new SocksWriter(PipeWriter.Create(_stream, writerOptions));
        }

        public void Dispose()
        {
            _stream.Dispose();
        }

        public Stream GetStream() => _stream;
    }
}