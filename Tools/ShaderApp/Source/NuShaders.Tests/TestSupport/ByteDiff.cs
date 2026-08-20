namespace NuShaders.Tests.TestSupport;

/// <summary>Byte-comparison helpers for round-trip tests, including masked compares for runtime-pointer fields.</summary>
public static class ByteDiff
{
    /// <summary>Index of the first differing byte, or -1 if equal. Differing lengths report at the shorter length.</summary>
    public static int FirstDifference(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        int n = Math.Min(a.Length, b.Length);
        for (int i = 0; i < n; i++)
            if (a[i] != b[i]) return i;
        return a.Length == b.Length ? -1 : n;
    }

    /// <summary>Human-readable diff summary for assertion messages.</summary>
    public static string Describe(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        if (a.Length != b.Length) return $"length {a.Length} vs {b.Length}";
        int d = FirstDifference(a, b);
        return d < 0 ? "identical" : $"first diff @0x{d:x}: {a[d]:x2} vs {b[d]:x2}";
    }

    /// <summary>Copy <paramref name="data"/> with the given [offset,length) ranges zeroed (for masking runtime-pointer fields).</summary>
    public static byte[] WithRangesZeroed(ReadOnlySpan<byte> data, params (int Offset, int Length)[] ranges)
    {
        var copy = data.ToArray();
        foreach (var (offset, length) in ranges)
            if (offset >= 0 && offset + length <= copy.Length)
                Array.Clear(copy, offset, length);
        return copy;
    }
}
