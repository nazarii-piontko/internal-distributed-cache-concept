using IDCC.Cache.Hashing;

namespace IDCC.Cache.Tests;

public sealed class Murmur3Tests
{
    /// <summary>
    /// Test the Mummur3 hash algorithm implementation.
    /// The test data is taken from https://en.wikipedia.org/wiki/MurmurHash
    /// </summary>
    [Theory]
    [InlineData("test", 0x9747b28c, 0x704b81dc)]
    [InlineData("Hello, world!", 0x9747b28c, 0x24884CBA)]
    [InlineData("The quick brown fox jumps over the lazy dog", 0x9747b28c, 0x2FA826CD)]
    public void Compute_ReturnsCorrectHash(string data, uint seed, uint expectedHash)
    {
        // Act
        var hash = Murmur3.Compute(data, seed);
        
        // Assert
        Assert.Equal(expectedHash, hash);
    }
}