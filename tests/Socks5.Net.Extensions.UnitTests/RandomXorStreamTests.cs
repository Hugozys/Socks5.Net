using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Socks5.Net.Security;
using Xunit;

namespace Socks5.Net.Extensions.UnitTests
{
    public class RandomXorStreamTests
    {
        [Fact]
        public async Task Async_ConsecutiveRead_ShouldSyncKeyStream()
        {
            // Arrange
            var memStream = new MemoryStream();
            var subjectUnderTests = new RandomXORStream(memStream,1,1);
            var rand = new Random();

            // Act
            for (int i = 0; i < 10; ++i)
            {
                var bytes = 1 << i;
                var testBytes = new byte[bytes];
                var readBytes = new byte[bytes];
                rand.NextBytes(testBytes);
                subjectUnderTests.Position = 0;
                await subjectUnderTests.WriteAsync(testBytes);

                subjectUnderTests.Position = 0;
                var readNum = await subjectUnderTests.ReadAsync(readBytes);
                readNum.Should().Be(bytes); 
                readBytes.Should().BeEquivalentTo(testBytes);
            }
        }

        [Fact]
        public async Task Async_WriteOnce_ReadMultipleTimes_ShouldSyncKeyStream()
        {
            // Arrange
            var mem = new MemoryStream();
            var subjectUnderTests = new RandomXORStream(mem, 1, 1);
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

         [Fact]
        public void Blocking_ConsecutiveRead_ShouldSyncKeyStream()
        {
            // Arrange
            var memStream = new MemoryStream();
            var subjectUnderTests = new RandomXORStream(memStream,1,1);
            var rand = new Random();

            // Act
            for (int i = 0; i < 10; ++i)
            {
                var bytes = 1 << i;
                var testBytes = new byte[bytes];
                var readBytes = new byte[bytes];
                rand.NextBytes(testBytes);
                subjectUnderTests.Position = 0;
                subjectUnderTests.Write(testBytes);

                subjectUnderTests.Position = 0;
                var readNum = subjectUnderTests.Read(readBytes);
                readNum.Should().Be(bytes); 
                readBytes.Should().BeEquivalentTo(testBytes);
            }
        }

        [Fact]
        public void Blocking_WriteOnce_ReadMultipleTimes_ShouldSyncKeyStream()
        {
            // Arrange
            var mem = new MemoryStream();
            var subjectUnderTests = new RandomXORStream(mem, 1, 1);
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
