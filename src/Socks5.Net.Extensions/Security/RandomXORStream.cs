using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Socks5.Net.Logging;

namespace Socks5.Net.Security
{
    public class RandomXORStream : Stream
    {
        private bool _disposed = false;

        private readonly Stream _baseStream;

        private readonly int _ingressSeed;

        private readonly int _egressSeed;
        private readonly Random _ingressRandom;

        private readonly Random _egressRandom;

        private readonly ILogger<RandomXORStream> _logger;

        public override bool CanRead => _baseStream.CanRead;

        public override bool CanSeek => _baseStream.CanSeek;

        public override bool CanWrite => _baseStream.CanWrite;

        public override long Length => _baseStream.Length;

        public override long Position { get => _baseStream.Position; set => _baseStream.Position = value; }

        public RandomXORStream(Stream stream, int ingressSeed, int egressSeed)
        {
            _baseStream = stream ?? throw new ArgumentNullException(nameof(stream));
            _ingressSeed = ingressSeed;
            _egressSeed = egressSeed;
            _ingressRandom = new Random(_ingressSeed);
            _egressRandom = new Random(_egressSeed);
            _logger = Socks.LoggerFactory?.CreateLogger<RandomXORStream>() ?? NoOpLogger<RandomXORStream>.Instance;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer.AsSpan().Slice(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            var readBytes = _baseStream.Read(buffer);
            if (readBytes == 0)
            {
                return 0;
            }

            Span<byte> randomBytes = stackalloc byte[readBytes];
            _ingressRandom.NextBytes(randomBytes);
            for (int i = 0; i < readBytes; ++i)
            {
                buffer[i] = (byte) (buffer[i]^randomBytes[i]);
            }
            return readBytes;

        }

        public override int ReadByte()
        {
            Span<byte> buffer = stackalloc byte[1];
            var eof = Read(buffer);
            return eof == 0 ? -1 : buffer[0];
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var cipherBytes = new byte[buffer.Length];
            var readBytes = await _baseStream.ReadAsync(cipherBytes, cancellationToken);
            var randomBytes = new byte[readBytes];
            _ingressRandom.NextBytes(randomBytes);
            
            for (int i = 0; i < readBytes; ++i)
            {
                randomBytes[i] = (byte)(cipherBytes[i] ^ randomBytes[i]);
            }

            randomBytes.CopyTo(buffer);
            return readBytes;

        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => ReadAsync(buffer.AsMemory().Slice(offset, count), cancellationToken).AsTask();

        public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);
        public override void SetLength(long value) => _baseStream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan().Slice(offset, count));
        public override void WriteByte(byte value)
        {
            ReadOnlySpan<byte> byteArray = stackalloc byte[1] { value };
            Write(byteArray);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Span<byte> randomBytes = stackalloc byte[buffer.Length];
            _egressRandom.NextBytes(randomBytes);
            for (int i = 0; i < buffer.Length; ++i)
            {
                randomBytes[i] = (byte) (randomBytes[i] ^ buffer[i]);
            }

            _baseStream.Write(randomBytes);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => WriteAsync(buffer.AsMemory().Slice(offset, count), cancellationToken).AsTask();
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var randomBytes = new byte[buffer.Length];
            var bufferSpan = buffer.Span;
            _egressRandom.NextBytes(randomBytes);
            for (int i = 0; i < buffer.Length; ++i)
            {
                randomBytes[i] = (byte) (randomBytes[i] ^ bufferSpan[i]);
            }
            return _baseStream.WriteAsync(randomBytes, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }
            if (disposing)
            {
                _baseStream?.Dispose();
            }

            _disposed = true;
            base.Dispose(disposing);
        }

        public override void Flush() => _baseStream.Flush();
    }
}