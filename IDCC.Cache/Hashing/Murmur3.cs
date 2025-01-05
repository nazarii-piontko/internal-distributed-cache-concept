namespace IDCC.Cache.Hashing;

internal static class Murmur3
{
    public static uint Compute(ReadOnlySpan<char> data, uint seed)
    {
        var length = data.Length;
        if (length == 0)
            return 0;
        
        uint hash = seed, k;

        int remainingBytes = length & 3;
        int iterationLen = length - remainingBytes, i = 0;
        for (; i < iterationLen; ++i)
        {
            k = ((uint)data[i] & 0xff) |
                (((uint)data[++i] & 0xff) << 8) |
                (((uint)data[++i] & 0xff) << 16) |
                (((uint)data[++i] & 0xff) << 24);
            hash ^= Murmur3Scramble(k);
            hash = (hash << 13) | (hash >> 19);
            hash = hash * 5 + 0xe6546b64;
        }

        k = remainingBytes switch
        {
            3 => ((uint)data[i] & 0xff) | (((uint)data[++i] & 0xff) << 8) | (((uint)data[++i] & 0xff) << 16),
            2 => ((uint)data[i] & 0xff) | (((uint)data[++i] & 0xff) << 8),
            1 => (uint)data[i] & 0xff,
            _ => 0
        };
        hash ^= Murmur3Scramble(k);
        
        // ReSharper disable once IntVariableOverflowInUncheckedContext
        hash ^= (uint)length;
        hash ^= hash >> 16;
        hash *= 0x85ebca6b;
        hash ^= hash >> 13;
        hash *= 0xc2b2ae35;
        hash ^= hash >> 16;
        
        return hash;
    }

    private static uint Murmur3Scramble(uint k) {
        k *= 0xcc9e2d51;
        k = (k << 15) | (k >> 17);
        k *= 0x1b873593;
        return k;
    }
}