using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NSec.Cryptography;
using NSec.Experimental;
using Socks5.Net.Logging;

namespace Socks5.Net.Security
{
    public class Chacha20Stream : Stream
    {
        private bool _disposed = false;
        private readonly Stream _baseStream;

        private readonly Key _chachaKey;

        private Nonce _ingress;

        private uint _inCounter;

        private int _inOffset;

        private Nonce _egress;

        private uint _outCounter;

        private int _outOffset;

        private readonly ILogger<Chacha20Stream> _logger;

        public override bool CanRead => _baseStream.CanRead;

        public override bool CanSeek => _baseStream.CanSeek;

        public override bool CanWrite => _baseStream.CanWrite;

        public override long Length => _baseStream.Length;

        public override long Position { get => _baseStream.Position; set => _baseStream.Position = value; }

        public Chacha20Stream(Stream stream, Key chachaKey, byte[] ingress, byte[] egress) : this(stream, chachaKey, ingress.AsSpan(), egress.AsSpan())
        {
        }

        public Chacha20Stream(Stream stream, Key chachaKey, ReadOnlySpan<byte> ingress, ReadOnlySpan<byte> egress)
        {
            _baseStream = stream ?? throw new ArgumentNullException(nameof(stream));
            _chachaKey = chachaKey ?? throw new ArgumentNullException(nameof(chachaKey));

            if (ingress.Length != Crypto.NonceFixedFieldSize)
            {
                throw new ArgumentException($"{nameof(ingress)} must be {Crypto.NonceFixedFieldSize} long");
            }

            if (egress.Length != Crypto.NonceFixedFieldSize)
            {
                throw new ArgumentException($"{nameof(egress)} must be {Crypto.NonceFixedFieldSize} long");
            }

            _ingress = new Nonce(ingress, Crypto.NonceCntSize);
            _inCounter = 0;
            _inOffset = 0;

            _egress = new Nonce(egress, Crypto.NonceCntSize);
            _outCounter = 0;
            _outOffset = 0;

            _logger = Socks.LoggerFactory?.CreateLogger<Chacha20Stream>() ?? NoOpLogger<Chacha20Stream>.Instance;
        }


        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer.AsSpan().Slice(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            Span<byte> encrypted = stackalloc byte[buffer.Length];
            var readBytes = _baseStream.Read(encrypted);
            encrypted = encrypted[..readBytes];

            if (_inOffset != 0)
            {
                int bytesToEndCipherBlock = (Crypto.Chacha20StreamBlockSize - _inOffset);
                bool offsetOverflow = bytesToEndCipherBlock < readBytes;
                int readTil = offsetOverflow ? bytesToEndCipherBlock : readBytes;
                Span<byte> temp = stackalloc byte[Crypto.Chacha20StreamBlockSize];

                encrypted[..readTil].CopyTo(temp.Slice(_inOffset));
                StreamCipherAlgorithm.ChaCha20.XOrIC(_chachaKey, _ingress, temp, temp, _inCounter);

                temp.Slice(_inOffset, readTil).CopyTo(buffer);
                encrypted = encrypted.Slice(readTil);
                buffer = buffer.Slice(readTil);

                _inOffset = offsetOverflow ? 0 : _inOffset + readBytes;
                if (offsetOverflow) { IncInCounter(); }

            }
            var bytes = encrypted.Length;
            var blockNum = bytes / Crypto.Chacha20StreamBlockSize;
            for (int i = 0; i < blockNum; ++i)
            {
                StreamCipherAlgorithm.ChaCha20.XOrIC(
                    _chachaKey,
                    _ingress,
                    encrypted[..Crypto.Chacha20StreamBlockSize],
                    buffer[..Crypto.Chacha20StreamBlockSize],
                    _inCounter);

                IncInCounter();
                buffer = buffer.Slice(Crypto.Chacha20StreamBlockSize);
                encrypted = encrypted.Slice(Crypto.Chacha20StreamBlockSize);
            }

            StreamCipherAlgorithm.ChaCha20.XOrIC(
                    _chachaKey,
                    _ingress,
                    encrypted,
                    buffer[..encrypted.Length],
                    _inCounter);

            _inOffset += encrypted.Length;

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
            Memory<byte> encrypted = new byte[buffer.Length];
            var readBytes = await _baseStream.ReadAsync(encrypted, cancellationToken);
            encrypted = encrypted[..readBytes];

            if (_inOffset != 0)
            {
                int bytesToEndCipherBlock = (Crypto.Chacha20StreamBlockSize - _inOffset);
                bool offsetOverflow = bytesToEndCipherBlock < readBytes;
                int readTil = offsetOverflow ? bytesToEndCipherBlock : readBytes;
                Memory<byte> temp = new byte[Crypto.Chacha20StreamBlockSize];

                encrypted[..readTil].CopyTo(temp.Slice(_inOffset));
                StreamCipherAlgorithm.ChaCha20.XOrIC(
                    _chachaKey,
                    _ingress,
                    temp.Span,
                    temp.Span,
                    _inCounter);

                temp.Slice(_inOffset, readTil).CopyTo(buffer);
                encrypted = encrypted.Slice(readTil);
                buffer = buffer.Slice(readTil);

                _inOffset = offsetOverflow ? 0 : _inOffset + readBytes;
                if (offsetOverflow) { IncInCounter(); }

            }
            var bytes = encrypted.Length;
            var blockNum = bytes / Crypto.Chacha20StreamBlockSize;
            for (int i = 0; i < blockNum; ++i)
            {
                StreamCipherAlgorithm.ChaCha20.XOrIC(
                    _chachaKey,
                    _ingress,
                    encrypted[..Crypto.Chacha20StreamBlockSize].Span,
                    buffer[..Crypto.Chacha20StreamBlockSize].Span,
                    _inCounter);

                IncInCounter();
                buffer = buffer.Slice(Crypto.Chacha20StreamBlockSize);
                encrypted = encrypted.Slice(Crypto.Chacha20StreamBlockSize);
            }

            StreamCipherAlgorithm.ChaCha20.XOrIC(
                    _chachaKey,
                    _ingress,
                    encrypted.Span,
                    buffer[..encrypted.Length].Span,
                    _inCounter);

            _inOffset += encrypted.Length;

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
            int writeBytes = buffer.Length;
            Span<byte> encrypted = stackalloc byte[writeBytes];
            Span<byte> writeBuffer = encrypted;
            if (_outOffset != 0)
            {
                int bytesToEndCipherBlock = (Crypto.Chacha20StreamBlockSize - _outOffset);
                bool offsetOverflow = bytesToEndCipherBlock < writeBytes;
                int writeTil = offsetOverflow ? bytesToEndCipherBlock : writeBytes;
                Span<byte> temp = stackalloc byte[Crypto.Chacha20StreamBlockSize];

                buffer[..writeTil].CopyTo(temp.Slice(_outOffset));

                StreamCipherAlgorithm.ChaCha20.XOrIC(
                    _chachaKey,
                    _egress,
                    temp,
                    temp,
                    _outCounter);

                temp.Slice(_outOffset, writeTil).CopyTo(writeBuffer);
                writeBuffer = writeBuffer.Slice(writeTil);
                buffer = buffer.Slice(writeTil);

                _outOffset = offsetOverflow ? 0 : _outOffset + writeBytes;
                if (offsetOverflow) { IncOutCounter(); }

            }

            var bytes = buffer.Length;
            var blockNum = bytes / Crypto.Chacha20StreamBlockSize;
            for (int i = 0; i < blockNum; ++i)
            {
                StreamCipherAlgorithm.ChaCha20.XOrIC(
                    _chachaKey,
                    _egress,
                    buffer[..Crypto.Chacha20StreamBlockSize],
                    writeBuffer[..Crypto.Chacha20StreamBlockSize],
                    _outCounter);

                IncOutCounter();
                writeBuffer = writeBuffer.Slice(Crypto.Chacha20StreamBlockSize);
                buffer = buffer.Slice(Crypto.Chacha20StreamBlockSize);
            }

            StreamCipherAlgorithm.ChaCha20.XOrIC(
                    _chachaKey,
                    _egress,
                    buffer,
                    writeBuffer,
                    _outCounter);

            _outOffset += writeBuffer.Length;

            _baseStream.Write(encrypted);

        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => WriteAsync(buffer.AsMemory().Slice(offset, count), cancellationToken).AsTask();
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int writeBytes = buffer.Length;
            Memory<byte> encrypted = new byte[writeBytes];
            Memory<byte> writeBuffer = encrypted;
            if (_outOffset != 0)
            {
                int bytesToEndCipherBlock = (Crypto.Chacha20StreamBlockSize - _outOffset);
                bool offsetOverflow = bytesToEndCipherBlock < writeBytes;
                int writeTil = offsetOverflow ? bytesToEndCipherBlock : writeBytes;
                Memory<byte> temp = new byte[Crypto.Chacha20StreamBlockSize];

                buffer[..writeTil].CopyTo(temp.Slice(_outOffset));

                StreamCipherAlgorithm.ChaCha20.XOrIC(
                    _chachaKey,
                    _egress,
                    temp.Span,
                    temp.Span,
                    _outCounter);

                temp.Slice(_outOffset, writeTil).CopyTo(writeBuffer);
                writeBuffer = writeBuffer.Slice(writeTil);
                buffer = buffer.Slice(writeTil);

                _outOffset = offsetOverflow ? 0 : _outOffset + writeBytes;

                if (offsetOverflow) { IncOutCounter(); }
            }

            var bytes = buffer.Length;
            var blockNum = bytes / Crypto.Chacha20StreamBlockSize;
            for (int i = 0; i < blockNum; ++i)
            {
                StreamCipherAlgorithm.ChaCha20.XOrIC(
                    _chachaKey,
                    _egress,
                    buffer[..Crypto.Chacha20StreamBlockSize].Span,
                    writeBuffer[..Crypto.Chacha20StreamBlockSize].Span,
                    _outCounter);

                IncOutCounter();
                writeBuffer = writeBuffer.Slice(Crypto.Chacha20StreamBlockSize);
                buffer = buffer.Slice(Crypto.Chacha20StreamBlockSize);
            }

            StreamCipherAlgorithm.ChaCha20.XOrIC(
                    _chachaKey,
                    _egress,
                    buffer.Span,
                    writeBuffer.Span,
                    _outCounter);

            _outOffset += writeBuffer.Length;

            return _baseStream.WriteAsync(encrypted);
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

        private void IncOutCounter()
        {
            ++_outCounter;
            if (_outCounter == 0)
            {
                Nonce.TryIncrement(ref _egress);
            }
        }

        private void IncInCounter()
        {
            ++_inCounter;
            if (_inCounter == 0)
            {
                Nonce.TryIncrement(ref _ingress);
            }
        }
    }
}