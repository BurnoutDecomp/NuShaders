using System.Buffers.Binary;
using NuShaders.Formats.BPR;

namespace NuShaders.CLI.Commands;

/// <summary>pack-bpr: compile a DXBC blob into a BPR ShaderProgramBuffer (primary + secondary).</summary>
internal static class PackBPRCommand
{
    public static int Run(ArgMap args)
    {
        string input = args.Require("input");
        var dxbc = File.ReadAllBytes(input);
        if (dxbc.Length < 32 || dxbc[0] != (byte)'D' || dxbc[1] != (byte)'X' || dxbc[2] != (byte)'B' || dxbc[3] != (byte)'C')
        {
            Console.Error.WriteLine($"not a DXBC blob: {input}");
            return 1;
        }

        int gap = args.GetInt("gap", 0x320);
        var primary = BPRShaderProgramBuffer.FromDXBC(dxbc, gap);
        byte[] primaryBytes = primary.ToBytes();
        byte[] secondary = BPRShaderProgramBuffer.PadSecondary(dxbc);

        // Diff-only mode: compare our generated primary against a stock one.
        if (args.Get("reference") is { } refPath)
        {
            var refBytes = File.ReadAllBytes(refPath);
            int n = Math.Min(primaryBytes.Length, refBytes.Length);
            int diffs = 0, first = -1;
            for (int i = 0; i < n; i++)
                if (primaryBytes[i] != refBytes[i]) { diffs++; if (first < 0) first = i; }
            string firstStr = first < 0 ? "-" : "0x" + first.ToString("x");
            Console.WriteLine($"diff vs {Path.GetFileName(refPath)}: ours={primaryBytes.Length}b ref={refBytes.Length}b diffs={diffs} first={firstStr} sizeDelta={primaryBytes.Length - refBytes.Length}");
            return 0;
        }

        string outDir = args.Get("out-dir") ?? Path.GetDirectoryName(Path.GetFullPath(input))!;
        string name = args.Get("name") ?? Path.GetFileNameWithoutExtension(input);

        // Optional: reuse a stock primary (patch only the DXBC size) instead of generating.
        if (args.Get("stock-primary-dir") is { } stockDir)
        {
            string stock = Path.Combine(stockDir, $"{name}_primary.dat");
            if (File.Exists(stock))
            {
                primaryBytes = File.ReadAllBytes(stock);
                BinaryPrimitives.WriteUInt32LittleEndian(primaryBytes.AsSpan(0x14), primary.DXBCSize);
                Console.WriteLine($"  primary: reused stock {name} (patched DXBC size)");
            }
        }

        Directory.CreateDirectory(outDir);
        string pri = Path.Combine(outDir, $"{name}_primary.dat");
        string sec = Path.Combine(outDir, $"{name}_secondary.dat");
        if (!args.Has("force") && (File.Exists(pri) || File.Exists(sec)))
        {
            Console.Error.WriteLine("output exists (use --force)");
            return 1;
        }

        File.WriteAllBytes(pri, primaryBytes);
        File.WriteAllBytes(sec, secondary);
        Console.WriteLine($"packed BPR {(primary.MType == 1 ? "PS" : "VS")}: primary={primaryBytes.Length}b secondary={secondary.Length}b -> {outDir}");
        return 0;
    }
}
