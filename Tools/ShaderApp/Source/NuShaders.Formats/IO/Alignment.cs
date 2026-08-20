namespace NuShaders.Formats.IO;

/// <summary>Power-of-two-agnostic alignment helpers used throughout the bundle formats.</summary>
public static class Alignment
{
    /// <summary>Round <paramref name="value"/> up to the next multiple of <paramref name="alignment"/>.</summary>
    public static int Align(int value, int alignment)
    {
        if (alignment <= 0) throw new ArgumentOutOfRangeException(nameof(alignment));
        int rem = value % alignment;
        return rem == 0 ? value : value + (alignment - rem);
    }
}
