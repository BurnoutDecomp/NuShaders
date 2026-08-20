namespace NuShaders.CLI;

/// <summary>Minimal "--key value" / "--flag" argument parser (dependency-free).</summary>
internal sealed class ArgMap
{
    private readonly Dictionary<string, string> _opts = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _flags = new(StringComparer.OrdinalIgnoreCase);

    public ArgMap(string[] args, int start)
    {
        for (int i = start; i < args.Length; i++)
        {
            string a = args[i];
            if (!a.StartsWith("--", StringComparison.Ordinal)) continue;
            string key = a[2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                _opts[key] = args[++i];
            else
                _flags.Add(key);
        }
    }

    public string? Get(string key) => _opts.TryGetValue(key, out var v) ? v : null;
    public string Require(string key) => Get(key) ?? throw new ArgumentException($"missing required option --{key}");
    public bool Has(string key) => _flags.Contains(key) || _opts.ContainsKey(key);

    public int GetInt(string key, int fallback)
    {
        string? s = Get(key);
        if (s is null) return fallback;
        return s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? Convert.ToInt32(s[2..], 16)
            : int.Parse(s);
    }
}
