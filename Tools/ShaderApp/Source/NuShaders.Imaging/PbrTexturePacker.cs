using BCnEncoder.Encoder;
using BCnEncoder.Shared;
using CommunityToolkit.HighPerformance;
using NuShaders.Formats.BPR;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace NuShaders.Imaging;

/// <summary>Per-channel PBR source maps (PNG paths). Only albedo is required; missing maps fall back to sensible constants.</summary>
public sealed record SourceMaps(
    string? AlbedoPath, string? AOPath, string? RoughnessPath,
    string? MetallicPath, string? DisplacementPath, string? OpacityPath = null);

public sealed record PackOptions(
    DXGIFormat DiffuseFormat = DXGIFormat.BC1_UNORM,
    DXGIFormat SpecFormat = DXGIFormat.BC3_UNORM,
    bool GenerateMips = true,
    int? ForceSize = null);

/// <summary>
/// Packs PBR source maps into the two textures the BPR PBR shader expects and BC-encodes them:
/// <list type="bullet">
/// <item>Diffuse RGBA = albedo.rgb + opacity (alpha = 1 if no opacity map).</item>
/// <item>Spec/ORMH RGBA = R:AO, G:roughness, B:metallic, A:height/displacement.</item>
/// </list>
/// Channel packing/resizing uses ImageSharp; BCn compression + mip generation uses BCnEncoder.Net (raw blocks,
/// no DDS header). BCnEncoder downsamples the assembled RGBA per channel, which is correct for the packed map.
/// </summary>
public static class PBRTexturePacker
{
    // Single contiguous backing buffer per image so we can index pixels linearly.
    private static readonly Configuration Config = CreateConfig();
    private static readonly DecoderOptions DecoderOpts = new() { Configuration = Config };

    private static Configuration CreateConfig()
    {
        var c = Configuration.Default.Clone();
        c.PreferContiguousImageBuffers = true;
        return c;
    }

    public static (EncodedSurface Diffuse, EncodedSurface Spec) Pack(SourceMaps maps, PackOptions options)
    {
        int size = options.ForceSize ?? DetectSize(maps);
        using var diffuse = BuildDiffuse(maps, size);
        using var spec = BuildSpec(maps, size);
        return (
            Encode(diffuse, options.DiffuseFormat, options.GenerateMips),
            Encode(spec, options.SpecFormat, options.GenerateMips));
    }

    /// <summary>Pack a single Diffuse texture (RGB albedo + opacity alpha) — for authoring one material slot.</summary>
    public static EncodedSurface PackDiffuse(string albedoPath, string? opacityPath, DXGIFormat format, bool generateMips = true, int? forceSize = null)
    {
        var maps = new SourceMaps(albedoPath, null, null, null, null, opacityPath);
        int size = forceSize ?? DetectSize(maps);
        using var img = BuildDiffuse(maps, size);
        return Encode(img, format, generateMips);
    }

    /// <summary>Pack a single Spec/ORMH texture (R:AO, G:roughness, B:metallic, A:height) — for authoring one material slot.</summary>
    public static EncodedSurface PackSpec(string? aoPath, string? roughnessPath, string? metallicPath, string? displacementPath, DXGIFormat format, bool generateMips = true, int? forceSize = null)
    {
        var maps = new SourceMaps(null, aoPath, roughnessPath, metallicPath, displacementPath, null);
        int size = forceSize ?? DetectSize(maps);
        using var img = BuildSpec(maps, size);
        return Encode(img, format, generateMips);
    }

    /// <summary>Pack a single grayscale Specular texture (R=G=B = the spec map, A=1) — for stock BPR Specular_*.fx shaders that read a .g mask.</summary>
    public static EncodedSurface PackSpecular(string specPath, DXGIFormat format, bool generateMips = true, int? forceSize = null)
    {
        int size = forceSize ?? DetectSize(new SourceMaps(specPath, null, null, null, null, null));
        using var img = BuildSpecular(specPath, size);
        return Encode(img, format, generateMips);
    }

    private static Image<Rgba32> BuildSpecular(string specPath, int size)
    {
        byte[]? spec = LoadChannelR(specPath, size);
        var img = new Image<Rgba32>(Config, size, size);
        var dst = Pixels(img);
        for (int i = 0; i < dst.Length; i++)
        {
            byte v = spec is null ? (byte)0 : spec[i];
            dst[i] = new Rgba32(v, v, v, 255);   // grayscale into R=G=B; A opaque
        }
        return img;
    }

    private static int DetectSize(SourceMaps m)
    {
        string probe = m.AlbedoPath ?? m.RoughnessPath ?? m.MetallicPath ?? m.AOPath ?? m.DisplacementPath
            ?? throw new ArgumentException("at least one source map is required");
        var info = Image.Identify(probe);
        int s = Math.Max(info.Width, info.Height);
        if (s <= 0) throw new InvalidDataException($"bad image size: {probe}");
        return s;
    }

    private static Image<Rgba32> BuildDiffuse(SourceMaps m, int size)
    {
        if (m.AlbedoPath is null) throw new ArgumentException("albedo map is required");
        using var albedo = LoadResized(m.AlbedoPath, size);
        byte[]? opacity = LoadChannelR(m.OpacityPath, size);

        var img = new Image<Rgba32>(Config, size, size);
        var src = Pixels(albedo);
        var dst = Pixels(img);
        for (int i = 0; i < dst.Length; i++)
        {
            // Opacity precedence: the explicit Opacity map (its red channel) overrides; otherwise the albedo's
            // own alpha is used (a PNG with no alpha channel loads as A=255, i.e. opaque, so this is safe).
            byte op = opacity is null ? src[i].A : opacity[i];
            dst[i] = new Rgba32(src[i].R, src[i].G, src[i].B, op);
        }
        return img;
    }

    private static Image<Rgba32> BuildSpec(SourceMaps m, int size)
    {
        byte[]? ao = LoadChannelR(m.AOPath, size);
        byte[]? rough = LoadChannelR(m.RoughnessPath, size);
        byte[]? metal = LoadChannelR(m.MetallicPath, size);
        byte[]? disp = LoadChannelR(m.DisplacementPath, size);

        var img = new Image<Rgba32>(Config, size, size);
        var dst = Pixels(img);
        for (int i = 0; i < dst.Length; i++)
            dst[i] = new Rgba32(
                ao is null ? (byte)255 : ao[i],        // R = AO (occlusion); 255 = no occlusion
                rough is null ? (byte)128 : rough[i],  // G = roughness
                metal is null ? (byte)0 : metal[i],    // B = metallic; 0 = dielectric
                disp is null ? (byte)255 : disp[i]);   // A = height/displacement; 255 = surface (no POM push-in)
        return img;
    }

    private static EncodedSurface Encode(Image<Rgba32> img, DXGIFormat format, bool mips)
    {
        var src = Pixels(img);
        var pixels = new ColorRgba32[src.Length];
        for (int i = 0; i < src.Length; i++)
            pixels[i] = new ColorRgba32(src[i].R, src[i].G, src[i].B, src[i].A);

        var encoder = new BcEncoder();
        encoder.OutputOptions.GenerateMipMaps = mips;
        encoder.OutputOptions.Quality = CompressionQuality.Balanced;
        encoder.OutputOptions.Format = ToCompressionFormat(format);

        var input = new ReadOnlyMemory2D<ColorRgba32>(pixels, img.Height, img.Width);
        byte[][] chain = encoder.EncodeToRawBytes(input);
        byte[] blocks = chain.SelectMany(c => c).ToArray();
        return new EncodedSurface(format, img.Width, img.Height, chain.Length, blocks);
    }

    private static CompressionFormat ToCompressionFormat(DXGIFormat f) => f switch
    {
        DXGIFormat.BC1_UNORM => CompressionFormat.Bc1,
        DXGIFormat.BC3_UNORM => CompressionFormat.Bc3,
        DXGIFormat.BC4_UNORM => CompressionFormat.Bc4,
        DXGIFormat.BC5_UNORM => CompressionFormat.Bc5,
        DXGIFormat.BC7_UNORM => CompressionFormat.Bc7,
        _ => throw new NotSupportedException($"unsupported encode format {f}"),
    };

    private static byte[]? LoadChannelR(string? path, int size)
    {
        if (path is null) return null;
        using var img = LoadResized(path, size);
        var src = Pixels(img);
        var buf = new byte[src.Length];
        for (int i = 0; i < buf.Length; i++) buf[i] = src[i].R;
        return buf;
    }

    private static Image<Rgba32> LoadResized(string path, int size)
    {
        var img = Image.Load<Rgba32>(DecoderOpts, path);
        if (img.Width != size || img.Height != size)
            img.Mutate(c => c.Resize(size, size));
        return img;
    }

    private static Span<Rgba32> Pixels(Image<Rgba32> img)
    {
        if (!img.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> mem))
            throw new InvalidOperationException("image buffer is not contiguous");
        return mem.Span;
    }
}
