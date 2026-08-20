using System.Buffers.Binary;
using NuShaders.Formats.BPR;

namespace NuShaders.CLI.Commands;

/// <summary>analyze: dump the decoded structure of a Shader (0x32) dat (read-only).</summary>
internal static class AnalyzeCommand
{
    public static int Run(ArgMap args)
    {
        string input = args.Require("input");
        var bytes = File.ReadAllBytes(input);

        if (bytes.Length < 0x30 || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != 0x30 || bytes[0x05] != 3)
        {
            Console.Error.WriteLine("analyze currently supports Shader (0x32) .dat only.");
            return 1;
        }

        var shader = BPRShaderResource.Read(bytes);
        var d = shader.Decode();

        Console.WriteLine($"Shader '{d.Name}': {shader.NumTechniques} technique(s), {d.Constants.Count} constant(s)");
        foreach (var t in d.Techniques)
        {
            Console.WriteLine($"  technique '{t.Name}': {t.Samplers.Count} sampler(s)");
            foreach (var s in t.Samplers)
                Console.WriteLine($"    s{s.Channel}: {s.Name}");
        }
        foreach (var c in d.Constants)
        {
            string inst = c.InstanceData is null ? "" : $" = ({string.Join(", ", c.InstanceData)})";
            Console.WriteLine($"  const[{c.Index,2}] {c.Name} size={c.Size} hash=0x{c.NameHash:x8}{inst}");
        }
        return 0;
    }
}
