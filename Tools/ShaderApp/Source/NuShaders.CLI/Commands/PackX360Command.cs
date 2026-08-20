using NuShaders.Formats.Xbox360;

namespace NuShaders.CLI.Commands;

/// <summary>pack-x360: pack an Xbox 360 Xenos blob into a ShaderProgramBuffer (big-endian).</summary>
internal static class PackX360Command
{
    public static int Run(ArgMap args)
    {
        string input = args.Require("input");
        var xenos = File.ReadAllBytes(input);
        string mode = args.Get("mode") ?? "generate";

        (byte[] primary, byte[] secondary) = mode switch
        {
            "split" => Xbox360ShaderProgramBuffer.PackSplit(xenos),
            "generate" => Xbox360ShaderProgramBuffer.PackGenerate(xenos, File.ReadAllText(args.Require("xsd"))),
            _ => throw new ArgumentException($"unknown --mode '{mode}' (expected split|generate)"),
        };

        string outDir = args.Get("out-dir") ?? Path.GetDirectoryName(Path.GetFullPath(input))!;
        string name = args.Get("name") ?? Path.GetFileNameWithoutExtension(input);
        Directory.CreateDirectory(outDir);
        string pri = Path.Combine(outDir, $"{name}_primary.dat");
        string sec = Path.Combine(outDir, $"{name}_secondary.dat");
        if (!args.Has("force") && (File.Exists(pri) || File.Exists(sec)))
        {
            Console.Error.WriteLine("output exists (use --force)");
            return 1;
        }

        File.WriteAllBytes(pri, primary);
        File.WriteAllBytes(sec, secondary);
        Console.WriteLine($"packed X360 ({mode}): primary={primary.Length}b secondary={secondary.Length}b -> {outDir}");
        return 0;
    }
}
