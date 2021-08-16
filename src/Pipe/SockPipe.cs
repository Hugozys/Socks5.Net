using System;
using System.IO;
using System.IO.Pipelines;
using Sock5.Net.Common;

namespace Sock5.Net.Pipe
{
    public sealed class SockPipe: IDisposable
    {
        private readonly Stream _stream;

        private readonly SockReader _reader;

        private readonly SockWriter _writer;

        public SockReader Reader => _reader;

        public SockWriter Writer => _writer;

        public SockPipe(Stream stream, StreamPipeReaderOptions? readerOptions = null, StreamPipeWriterOptions? writerOptions = null)
        {
            if (!stream.CanRead || !stream.CanWrite)
            {
                throw new ArgumentException($"{nameof(stream)} must be readable and writable");
            }
            _stream = stream;
            _reader = new SockReader(PipeReader.Create(_stream, readerOptions));
            _writer = new SockWriter(PipeWriter.Create(_stream, writerOptions));
        }

        public void Dispose()
        {
            _stream.Dispose();
        }

        public Stream GetStream() => _stream;
    }
}