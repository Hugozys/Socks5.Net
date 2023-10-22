using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using System.Reflection;
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
            Memory<byte> nonce = new byte[Crypto.NonceSize];
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
                await subjectUnderTests.WriteAsync(testBytes.AsMemory()[..byteChunk]);

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
            Memory<byte> nonce = new byte[Crypto.NonceSize];
            rand.NextBytes(nonce.Span);
            var mem = new MemoryStream();
            var subjectUnderTests = new Chacha20Stream(mem, key, nonce.Span, nonce.Span);
            // 1: read 63 bytes using counter 1 -> offset 1
            // 2: read 1  byte  using counter 1  
            //    read 64 bytes using counter 2
            //    read 4  bytes using counter 3 -> offset 60 
            // 3. read 60 bytes using counter 3
            //    read 64 bytes using counter 4
            //    read 64 bytes using counter 5
            //    read 5  bytes using counter 6 -> offset 59
            // 4. read 4 bytes using counter  6 -> offset 55
            List<int> readSequence = new(){63, 68, 193, 4};
            var writeNum = readSequence.Aggregate(0, (acc, ele) => acc + ele);
            Memory<byte> writeBytes = new byte[writeNum];
            rand.NextBytes(writeBytes.Span);
            Memory<byte> decryptedBytes = new byte[writeNum];
            var it = decryptedBytes;
            await subjectUnderTests.WriteAsync(writeBytes);
            subjectUnderTests.Position = 0;
            foreach (var read in readSequence)
            {
                var readBytes = await subjectUnderTests.ReadAsync(it[..read]);
                readBytes.Should().Be(read);
                it = it[readBytes..];
            }

            writeBytes.ToArray().Should().BeEquivalentTo(decryptedBytes.ToArray());
        }

        [Fact]
        public async Task Async_ReadOnce_WriteMultipleTimes_ShouldSyncKeyStream()
        {
            // Arrange
            var key = Key.Create(StreamCipherAlgorithm.ChaCha20);
            var rand = new Random();
            Memory<byte> nonce = new byte[Crypto.NonceSize];
            rand.NextBytes(nonce.Span);
            var mem = new MemoryStream();
            var subjectUnderTests = new Chacha20Stream(mem, key, nonce.Span, nonce.Span);
            // 1: write 63 bytes using counter 1 -> offset 1
            // 2: write 1  byte  using counter 1  
            //    write 64 bytes using counter 2
            //    write 4  bytes using counter 3 -> offset 60 
            // 3. write 60 bytes using counter 3
            //    write 64 bytes using counter 4
            //    write 64 bytes using counter 5
            //    write 5  bytes using counter 6 -> offset 59
            // 4. read 4 bytes using counter  6 -> offset 55
            List<int> writeSequence = new(){63, 68, 193, 4};
            var readNum = writeSequence.Aggregate(0, (acc, ele) => acc + ele);
            Memory<byte> readBuffer = new byte[readNum];
            Memory<byte> writeBuffer = new byte[readNum];
            var it = writeBuffer;
            rand.NextBytes(writeBuffer.Span);
            foreach (var write in writeSequence)
            {
                await subjectUnderTests.WriteAsync(it[..write]);
                it = it[write..];
            }
            subjectUnderTests.Position = 0;
            var readBytes = await subjectUnderTests.ReadAsync(readBuffer);
            readBytes.Should().Be(readNum);
            readBuffer.ToArray().Should().BeEquivalentTo(writeBuffer.ToArray());
        }

        [Fact]
        public async Task Async_CounterOverflow_ShouldIncrementNonce()
        {
            // Arrange
            var key = Key.Create(StreamCipherAlgorithm.ChaCha20);
            var rand = new Random();
            Memory<byte> nonce = new byte[Crypto.NonceSize];
            rand.NextBytes(nonce.Span);
            var mem = new MemoryStream();
            var subjectUnderTests = new Chacha20Stream(mem, key, nonce.Span, nonce.Span);

            subjectUnderTests
                .GetType()
                .GetField(
                    "_inCounter", 
                    BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(subjectUnderTests, uint.MaxValue);

            subjectUnderTests
                .GetType()
                .GetField(
                    "_outCounter", 
                    BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(subjectUnderTests, uint.MaxValue);
    
            List<int> writeSequence = new(){63, 68, 193, 4};
            var readNum = writeSequence.Aggregate(0, (acc, ele) => acc + ele);
            Memory<byte> readBuffer = new byte[readNum];
            Memory<byte> writeBuffer = new byte[readNum];
            var it = writeBuffer;
            rand.NextBytes(writeBuffer.Span);
            foreach (var write in writeSequence)
            {
                await subjectUnderTests.WriteAsync(it[..write]);
                it = it[write..];
            }
            subjectUnderTests.Position = 0;
            var readBytes = await subjectUnderTests.ReadAsync(readBuffer);
            readBytes.Should().Be(readNum);
            readBuffer.ToArray().Should().BeEquivalentTo(writeBuffer.ToArray());
        }

        

        [Theory]
        [InlineData(Crypto.Chacha20StreamBlockSize - 1)]
        [InlineData(2*Crypto.Chacha20StreamBlockSize - 1)]
        public void Blocking_NonIntegralMultipleOfBlockSize_ShouldSyncKeyStream(int byteChunk)
        {
            // Arrange
            var key = Key.Create(StreamCipherAlgorithm.ChaCha20);
            var rand = new Random();
            Memory<byte> nonce = new byte[Crypto.NonceSize];
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
                subjectUnderTests.Write(testBytes.AsSpan()[..byteChunk]);

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
            Span<byte> nonce = new byte[Crypto.NonceSize];
            rand.NextBytes(nonce);
            var mem = new MemoryStream();
            var subjectUnderTests = new Chacha20Stream(mem, key, nonce, nonce);
            List<int> readSequence = new(){63, 68, 193, 4};
            var writeNum = readSequence.Aggregate(0, (acc, ele) => acc + ele);
            Span<byte> writeBytes = new byte[writeNum];
            Span<byte> decryptedBytes = new byte[writeNum];
            var it = decryptedBytes;
            subjectUnderTests.Write(writeBytes);
            subjectUnderTests.Position = 0;
            foreach (var read in readSequence)
            {
                var readBytes = subjectUnderTests.Read(decryptedBytes[..read]);
                it = it[readBytes..];
                readBytes.Should().Be(read);
            }

            writeBytes.ToArray().Should().BeEquivalentTo(decryptedBytes.ToArray());
        }

        [Fact]
        public void Blocking_ReadOnce_WriteMultipleTimes_ShouldSyncKeyStream()
        {
            // Arrange
            var key = Key.Create(StreamCipherAlgorithm.ChaCha20);
            var rand = new Random();
            Memory<byte> nonce = new byte[Crypto.NonceSize];
            rand.NextBytes(nonce.Span);
            var mem = new MemoryStream();
            var subjectUnderTests = new Chacha20Stream(mem, key, nonce.Span, nonce.Span);
            List<int> writeSequence = new(){63, 68, 193, 4};
            var readNum = writeSequence.Aggregate(0, (acc, ele) => acc + ele);
            Span<byte> readBuffer = new byte[readNum];
            Span<byte> writeBuffer = new byte[readNum];
            rand.NextBytes(writeBuffer);
            var it = writeBuffer;
            foreach (var write in writeSequence)
            {
                subjectUnderTests.Write(it[..write]);
                it = it[write..];
            }
            subjectUnderTests.Position = 0;
            var readBytes = subjectUnderTests.Read(readBuffer);
            readBytes.Should().Be(readNum);
            readBuffer.ToArray().Should().BeEquivalentTo(writeBuffer.ToArray());
        }

        [Fact]
        public void Blocking_CounterOverflow_ShouldIncrementNonce()
        {
            // Arrange
            var key = Key.Create(StreamCipherAlgorithm.ChaCha20);
            var rand = new Random();
            Memory<byte> nonce = new byte[Crypto.NonceSize];
            rand.NextBytes(nonce.Span);
            var mem = new MemoryStream();
            var subjectUnderTests = new Chacha20Stream(mem, key, nonce.Span, nonce.Span);

            subjectUnderTests
                .GetType()
                .GetField(
                    "_inCounter", 
                    BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(subjectUnderTests, uint.MaxValue);

            subjectUnderTests
                .GetType()
                .GetField(
                    "_outCounter", 
                    BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(subjectUnderTests, uint.MaxValue);

            List<int> writeSequence = new(){63, 68, 193, 4};
            var readNum = writeSequence.Aggregate(0, (acc, ele) => acc + ele);
            Span<byte> readBuffer = new byte[readNum];
            Span<byte> writeBuffer = new byte[readNum];
            rand.NextBytes(writeBuffer);
            var it = writeBuffer;
            foreach (var write in writeSequence)
            {
                subjectUnderTests.Write(it[..write]);
                it = it[write..];
            }
            subjectUnderTests.Position = 0;
            var readBytes = subjectUnderTests.Read(readBuffer);
            readBytes.Should().Be(readNum);
            readBuffer.ToArray().Should().BeEquivalentTo(writeBuffer.ToArray());
        }
    }
}
