using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NSec.Cryptography;
using NSec.Experimental;
using Socks5.Net.Security;
using Xunit;

namespace Socks5.Net.Extensions.UnitTests
{
    public class Chacha20StreamTests
    {
        [Theory]
        [InlineData(Crypto.Chacha20StreamBlockSize - 1)]
        [InlineData(2*Crypto.Chacha20StreamBlockSize - 1)]
        public async Task Async_NonIntegralMultipleOfBlockSize_ShouldSyncKeyStream(int byteChunk)
        {
            // Arrange
            var key = Key.Create(StreamCipherAlgorithm.ChaCha20);
            var rand = new Random();
            Memory<byte> nonce = new byte[Crypto.NonceFixedFieldSize];
            rand.NextBytes(nonce.Span);
            var memStream = new MemoryStream();
            var subjectUnderTests = new Chacha20Stream(memStream, key, nonce.Span, nonce.Span);
            var testBytes = new byte[byteChunk];
            var readBuffer = new byte[byteChunk];

            // Act
            for (int i = 0; i < 10; ++i)
            {
                rand.NextBytes(testBytes);
                subjectUnderTests.Position = 0;
                await subjectUnderTests.WriteAsync(testBytes[..byteChunk]);

                subjectUnderTests.Position = 0;
                var readBytes = await subjectUnderTests.ReadAsync(readBuffer);
                readBytes.Should().Be(byteChunk); 
                readBuffer.Should().BeEquivalentTo(testBytes);
            }
        }

        [Fact]
        public async Task Async_WriteOnce_ReadMultipleTimes_ShouldSyncKeyStream()
        {
            // Arrange
            var key = Key.Create(StreamCipherAlgorithm.ChaCha20);
            var rand = new Random();
            Memory<byte> nonce = new byte[Crypto.NonceFixedFieldSize];
            rand.NextBytes(nonce.Span);
            var mem = new MemoryStream();
            var subjectUnderTests = new Chacha20Stream(mem, key, nonce.Span, nonce.Span);
            List<int> readSequence = new List<int>(){63, 68, 129};
            var writeNum = readSequence.Aggregate(0, (acc, ele) => acc + ele);
            Memory<byte> writeBytes = new byte[writeNum];
            Memory<byte> decryptedBytes = new byte[writeNum];
            var it = decryptedBytes;
            await subjectUnderTests.WriteAsync(writeBytes);
            subjectUnderTests.Position = 0;
            foreach (var read in readSequence)
            {
                var readBytes = await subjectUnderTests.ReadAsync(decryptedBytes.Slice(0, read));
                it = it.Slice(readBytes);
                readBytes.Should().Be(read);
            }

            writeBytes.ToArray().Should().BeEquivalentTo(decryptedBytes.ToArray());
        }

        [Theory]
        [InlineData(Crypto.Chacha20StreamBlockSize - 1)]
        [InlineData(2*Crypto.Chacha20StreamBlockSize - 1)]
        public void Blocking_NonIntegralMultipleOfBlockSize_ShouldSyncKeyStream(int byteChunk)
        {
            // Arrange
            var key = Key.Create(StreamCipherAlgorithm.ChaCha20);
            var rand = new Random();
            Memory<byte> nonce = new byte[Crypto.NonceFixedFieldSize];
            rand.NextBytes(nonce.Span);
            var memStream = new MemoryStream();
            var subjectUnderTests = new Chacha20Stream(memStream, key, nonce.Span, nonce.Span);
            var testBytes = new byte[byteChunk];
            var readBuffer = new byte[byteChunk];

            // Act
            for (int i = 0; i < 10; ++i)
            {
                rand.NextBytes(testBytes);
                subjectUnderTests.Position = 0;
                subjectUnderTests.Write(testBytes[..byteChunk]);

                subjectUnderTests.Position = 0;
                var readBytes = subjectUnderTests.Read(readBuffer);
                readBytes.Should().Be(byteChunk); 
                readBuffer.Should().BeEquivalentTo(testBytes);
            }
        }

        [Fact]
        public void Blocking_WriteOnce_ReadMultipleTimes_ShouldSyncKeyStream()
        {
            // Arrange
            var key = Key.Create(StreamCipherAlgorithm.ChaCha20);
            var rand = new Random();
            Span<byte> nonce = new byte[Crypto.NonceFixedFieldSize];
            rand.NextBytes(nonce);
            var mem = new MemoryStream();
            var subjectUnderTests = new Chacha20Stream(mem, key, nonce, nonce);
            List<int> readSequence = new List<int>(){63, 68, 129};
            var writeNum = readSequence.Aggregate(0, (acc, ele) => acc + ele);
            Span<byte> writeBytes = new byte[writeNum];
            Span<byte> decryptedBytes = new byte[writeNum];
            var it = decryptedBytes;
            subjectUnderTests.Write(writeBytes);
            subjectUnderTests.Position = 0;
            foreach (var read in readSequence)
            {
                var readBytes = subjectUnderTests.Read(decryptedBytes.Slice(0, read));
                it = it.Slice(readBytes);
                readBytes.Should().Be(read);
            }

            writeBytes.ToArray().Should().BeEquivalentTo(decryptedBytes.ToArray());
        }
    }
}
