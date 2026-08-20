using System.Text.Json;

namespace NuShaders.Formats.Bundle;

/// <summary>
/// Authoritative id→name fallback from the repo's <c>Reference/ResourceDB.json</c> (a flat map of lowercase-hex
/// crc32 id → gamedb/friendly name, ≈104k entries). Lazy-loaded once and cached. Used when a bundle's <c>.debug.xml</c>
/// lacks a name for a resource (or is missing entirely). Returns null when the file can't be located (e.g. a build run
/// outside the repo) — callers then fall back to the hex id.
/// </summary>
public sealed class ResourceNameDB
{
    public static ResourceNameDB Instance { get; } = new();

    private readonly Lazy<IReadOnlyDictionary<string, string>> _map = new(Load);

    public string? Lookup(uint id) => _map.Value.GetValueOrDefault(id.ToString("x8"));

    private static IReadOnlyDictionary<string, string> Load()
    {
        string? path = Locate();
        if (path is null) return EmptyDictionary();
        try
        {
            using var fs = File.OpenRead(path);
            var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(fs);
            if (raw is null) return EmptyDictionary();
            var map = new Dictionary<string, string>(raw.Count, StringComparer.Ordinal);
            foreach (var (k, v) in raw)
            {
                string key = k.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? k[2..] : k;
                map[key.ToLowerInvariant()] = v;
            }
            return map;
        }
        catch { return EmptyDictionary(); }
    }

    private static Dictionary<string, string> EmptyDictionary() => new(StringComparer.Ordinal);

    private static string? Locate()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "Reference", "ResourceDB.json");
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }
}
