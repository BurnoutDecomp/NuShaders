namespace NuShaders.Tests.TestSupport;

/// <summary>
/// Locates the repo's <c>Reference/</c> corpus from the test assembly location so round-trip
/// theories can enumerate real bundle resources. Tests skip (return early) when it's absent.
/// </summary>
public static class ReferencePaths
{
    public static string? RepoRoot { get; } = FindRepoRoot();

    public static bool Available => RepoRoot is not null;

    public static string ReferenceDir => Path.Combine(RepoRoot!, "Reference");
    public static string BPRShadersDir => Path.Combine(ReferenceDir, "BPR", "SHADERS");
    public static string BPRShaderProgramBufferDir => Path.Combine(BPRShadersDir, "ShaderProgramBuffer");
    public static string BPRShaderDir => Path.Combine(BPRShadersDir, "Shader");
    public static string BPRTextureDir => Path.Combine(BPRShadersDir, "Texture");
    public static string BPRTextureStateDir => Path.Combine(BPRShadersDir, "TextureState");
    public static string BPRMetaPath => Path.Combine(BPRShadersDir, ".meta.yaml");
    /// <summary>Root of the per-version X360 corpora: &lt;this&gt;/{Breaker,1.6,1.8}/SHADERS/...</summary>
    public static string X360ShadersDir => Path.Combine(ReferenceDir, "360", "Shaders");
    public static string PBRMatsDir => Path.Combine(RepoRoot!, "Materials");

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Reference")) &&
                Directory.Exists(Path.Combine(dir.FullName, "Source")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
