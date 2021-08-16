using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Sock5.Net.UnitTests
{
    public class DelayedMemoryStream : Stream
    {
        private readonly MemoryStream _baseStream;

        private readonly int _millisecondsDelay;

        public DelayedMemoryStream(byte[] payload, int millisecondsDelay = 100): this(millisecondsDelay)
        {
            _baseStream = new MemoryStream(payload);
        }

        public DelayedMemoryStream(int millisecondsDelay = 100)
        {
            _baseStream = new MemoryStream();
            _millisecondsDelay = millisecondsDelay;
        }


        public override bool CanRead => _baseStream.CanRead;

        public override bool CanSeek => _baseStream.CanSeek;

        public override bool CanWrite => _baseStream.CanWrite;

        public override long Length => _baseStream.Length;

        public override long Position { get => _baseStream.Position; set => _baseStream.Position = value; }

        public override void Flush() => _baseStream.Flush();
        
        public override int Read(byte[] buffer, int offset, int count)
        {
            var span = buffer.AsSpan().Slice(offset, count);
            return Read(span);
        }


        public override int Read(Span<byte> buffer)
        {
            Thread.Sleep(_millisecondsDelay);
            return _baseStream.Read(buffer);
        }

       public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(_millisecondsDelay);
            return await _baseStream.ReadAsync(buffer, cancellationToken);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            var memory = buffer.AsMemory().Slice(offset, count);
            return ReadAsync(memory, cancellationToken).AsTask();
        }

        public override int ReadByte()
        {
            Span<byte> buffer = stackalloc byte[1];
            var eof = Read(buffer) == 0;
            return eof ? -1 : buffer[0];
        }

        public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);

        public override void SetLength(long value) => _baseStream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
        {
            var span = buffer.AsSpan().Slice(offset, count);
            Write(span);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Thread.Sleep(_millisecondsDelay);
            _baseStream.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            Span<byte> span = stackalloc byte[1] {value};
            Write(span);
        }
    }
}