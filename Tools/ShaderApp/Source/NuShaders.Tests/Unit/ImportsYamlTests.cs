using NuShaders.Formats.Bundle;
using NuShaders.Tests.TestSupport;

namespace NuShaders.Tests.Unit;

public class ImportsYAMLTests
{
    [Fact]
    public void Reemits_reference_imports_byte_identical()
    {
        if (!ReferencePaths.Available) return;
        var dir = ReferencePaths.BPRShaderDir;
        if (!Directory.Exists(dir)) return;

        var files = Directory.GetFiles(dir, "*_imports.yaml");
        Assert.NotEmpty(files);

        var failures = new List<string>();
        foreach (var f in files)
        {
            string original = File.ReadAllText(f);
            string reemitted = ImportsYAML.Read(original).ToYAML();
            if (original != reemitted) failures.Add(Path.GetFileName(f));
        }

        Assert.True(failures.Count == 0,
            $"{failures.Count}/{files.Length} imports.yaml re-emitted differently: {string.Join(", ", failures.Take(10))}");
    }
}
