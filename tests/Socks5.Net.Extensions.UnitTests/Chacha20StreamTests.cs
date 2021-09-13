using System;
using System.IO;
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
        [InlineData(2*Crypto.Chacha20StreamBlockSize - 1)] // (64, 63), (1, 64, 62), (2, 64, 61) 
        public async Task NonIntegralMultipleOfBlockSize_ShouldSyncKeyStream(int byteChunk)
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
        public async Task WriteOnce_ReadMultiple_ShouldSyncKeyStream()
        {
            // Arrange
            var key = Key.Create(StreamCipherAlgorithm.ChaCha20);
            var rand = new Random();
            Memory<byte> nonce = new byte[Crypto.NonceFixedFieldSize];
            rand.NextBytes(nonce.Span);
            var mem = new MemoryStream();
            var subjectUnderTests = new Chacha20Stream(mem, key, nonce.Span, nonce.Span);
            var size = Crypto.Chacha20StreamBlockSize*3;
            Memory<byte> writeBytes = new byte[size];
            Memory<byte> decryptedBytes = new byte[size];
            var it = decryptedBytes;
            await subjectUnderTests.WriteAsync(writeBytes);
            subjectUnderTests.Position = 0;
            for (int i = 0; i < size / 8; ++i)
            {
                var readBytes = await subjectUnderTests.ReadAsync(decryptedBytes.Slice(0, 8));
                it = it.Slice(8);
                readBytes.Should().Be(8);
            }

            writeBytes.ToArray().Should().BeEquivalentTo(decryptedBytes.ToArray());
            
        }
    }
}
