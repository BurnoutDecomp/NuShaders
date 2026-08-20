using NuShaders.Formats.BPR;
using NuShaders.Formats.Bundle;
using NuShaders.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace NuShaders.Tests.Unit;

public class PBRTexturePackerTests
{
    [Fact]
    public void Pack_produces_consistent_bc_surfaces_and_a_full_resource_set()
    {
        var dir = Directory.CreateTempSubdirectory("nupack");
        try
        {
            const int size = 8;
            string albedo = MakePng(dir.FullName, "albedo", size, new Rgba32(200, 180, 160, 255));
            string ao = MakePng(dir.FullName, "ao", size, new Rgba32(255, 255, 255, 255));
            string rough = MakePng(dir.FullName, "rough", size, new Rgba32(120, 120, 120, 255));
            string metal = MakePng(dir.FullName, "metal", size, new Rgba32(255, 255, 255, 255));
            string disp = MakePng(dir.FullName, "disp", size, new Rgba32(64, 64, 64, 255));

            var (diff, spec) = PBRTexturePacker.Pack(new SourceMaps(albedo, ao, rough, metal, disp), new PackOptions());

            Assert.Equal(DXGIFormat.BC1_UNORM, diff.Format);
            Assert.Equal(DXGIFormat.BC3_UNORM, spec.Format);
            foreach (var s in new[] { diff, spec })
            {
                Assert.Equal(size, s.Width);
                Assert.Equal(size, s.Height);
                Assert.True(s.MipLevels >= 1);
                // The concatenated block stream length must equal the mip-chain byte size for the reported mips.
                Assert.Equal(s.Format.MipChainBytes(s.Width, s.Height, s.MipLevels), s.BCBlocks.Length);
            }

            // The set builds 4 resources (2 textures + 2 texturestates) with the diffuse texturestate importing the diffuse texture.
            var res = BPRTextureSet.Build(new TextureSetInputs("UnitMat", diff, spec));
            Assert.Equal(4, res.Count);
            Assert.Equal(2, res.Count(r => r.MetaType == BPRTextureResource.MetaType));
            Assert.Equal(2, res.Count(r => r.MetaType == BPRTextureState.MetaType));
        }
        finally { dir.Delete(true); }
    }

    [Fact]
    public void PackDiffuse_plus_BuildSingle_makes_one_texture_and_one_texturestate()
    {
        var dir = Directory.CreateTempSubdirectory("nusingle");
        try
        {
            const int size = 8;
            string albedo = MakePng(dir.FullName, "albedo", size, new Rgba32(200, 180, 160, 255));
            var surf = PBRTexturePacker.PackDiffuse(albedo, null, DXGIFormat.BC1_UNORM);
            Assert.Equal(DXGIFormat.BC1_UNORM, surf.Format);
            Assert.Equal(size, surf.Width);
            Assert.Equal(surf.Format.MipChainBytes(surf.Width, surf.Height, surf.MipLevels), surf.BCBlocks.Length);

            var res = BPRTextureSet.BuildSingle("UnitMat", "Diffuse", surf);
            Assert.Equal(2, res.Count);
            var tex = res.Single(r => r.MetaType == BPRTextureResource.MetaType);
            var ts = res.Single(r => r.MetaType == BPRTextureState.MetaType);
            Assert.Contains(ts.Imports!.Entries, e => e.Id == tex.Id.Value);   // texturestate imports the texture
        }
        finally { dir.Delete(true); }
    }

    [Fact]
    public void Single_texture_merges_into_a_bundle_and_appears_in_the_catalog()
    {
        var dir = Directory.CreateTempSubdirectory("nuintmerge");
        try
        {
            // No .meta.yaml yet — MergeIntoBundle synthesizes a proper bundle header (as it does for a fresh bundle).
            string albedo = MakePng(dir.FullName, "albedo", 8, new Rgba32(180, 160, 140, 255));
            var surf = PBRTexturePacker.PackDiffuse(albedo, null, DXGIFormat.BC1_UNORM);
            var resources = BPRTextureSet.BuildSingle("IntMat", "Diffuse", surf);
            TextureSetWriter.MergeIntoBundle(dir.FullName, resources);

            var cat = BundleCatalog.Open(dir.FullName);
            var ts = resources.Single(r => r.MetaType == BPRTextureState.MetaType);
            Assert.Contains(cat.TextureStates, r => r.Id.Value == ts.Id.Value);   // assignable in the editor's pick-list
            Assert.NotEmpty(cat.Textures);
        }
        finally { dir.Delete(true); }
    }

    [Fact]
    public void Thumbnail_decodes_a_packed_texture_to_approximately_the_source_color()
    {
        var dir = Directory.CreateTempSubdirectory("nuthumb");
        try
        {
            string albedo = MakePng(dir.FullName, "albedo", 16, new Rgba32(200, 100, 50, 255));
            var surf = PBRTexturePacker.PackDiffuse(albedo, null, DXGIFormat.BC1_UNORM);
            var (primary, secondary) = BPRTextureResource.Build(surf.Format, surf.Width, surf.Height, surf.MipLevels, surf.BCBlocks);

            var thumb = TextureThumbnail.Decode(primary, secondary, maxSize: 64);
            Assert.NotNull(thumb);
            Assert.True(thumb!.Width >= 4 && thumb.Height >= 4);
            int idx = ((thumb.Height / 2) * thumb.Width + thumb.Width / 2) * 4;
            Assert.InRange(thumb.Rgba[idx + 0], 175, 225);   // R ~200 (BC1 lossy)
            Assert.InRange(thumb.Rgba[idx + 1], 75, 125);    // G ~100
            Assert.InRange(thumb.Rgba[idx + 2], 25, 75);     // B ~50
            Assert.Equal(255, thumb.Rgba[idx + 3]);          // A opaque
        }
        finally { dir.Delete(true); }
    }

    [Fact]
    public void PackSpecular_makes_a_grayscale_texture()
    {
        var dir = Directory.CreateTempSubdirectory("nuspec");
        try
        {
            string spec = MakePng(dir.FullName, "spec", 16, new Rgba32(96, 96, 96, 255));
            var surf = PBRTexturePacker.PackSpecular(spec, DXGIFormat.BC1_UNORM);
            var (primary, secondary) = BPRTextureResource.Build(surf.Format, surf.Width, surf.Height, surf.MipLevels, surf.BCBlocks);

            var thumb = TextureThumbnail.Decode(primary, secondary, maxSize: 64);
            Assert.NotNull(thumb);
            int idx = ((thumb!.Height / 2) * thumb.Width + thumb.Width / 2) * 4;
            Assert.InRange(thumb.Rgba[idx + 0], 76, 116);   // R ~96
            Assert.InRange(thumb.Rgba[idx + 1], 76, 116);   // G ~96 (= R)
            Assert.InRange(thumb.Rgba[idx + 2], 76, 116);   // B ~96 (= R)
            Assert.Equal(255, thumb.Rgba[idx + 3]);         // A opaque

            var res = BPRTextureSet.BuildSingle("M", "Specular", surf);
            Assert.Equal(2, res.Count);
            Assert.Single(res, r => r.MetaType == BPRTextureResource.MetaType);
            Assert.Single(res, r => r.MetaType == BPRTextureState.MetaType);
        }
        finally { dir.Delete(true); }
    }

    [Fact]
    public void DDSWriter_wraps_a_bc1_texture_with_a_dxt10_header()
    {
        var dir = Directory.CreateTempSubdirectory("nudds");
        try
        {
            string albedo = MakePng(dir.FullName, "a", 16, new Rgba32(180, 160, 140, 255));
            var surf = PBRTexturePacker.PackDiffuse(albedo, null, DXGIFormat.BC1_UNORM);
            var (primary, secondary) = BPRTextureResource.Build(surf.Format, surf.Width, surf.Height, surf.MipLevels, surf.BCBlocks);
            var hdr = BPRTextureResource.ReadPrimary(primary);

            var dds = DDSWriter.Build(hdr, secondary);
            Assert.Equal(0x20534444u, BitConverter.ToUInt32(dds, 0));                         // 'DDS '
            Assert.Equal(124u, BitConverter.ToUInt32(dds, 4));                                // dwSize
            Assert.Equal(0x30315844u, BitConverter.ToUInt32(dds, 4 + 80));                    // ddspf fourCC 'DX10'
            Assert.Equal((uint)DXGIFormat.BC1_UNORM, BitConverter.ToUInt32(dds, 4 + 124));    // DXT10 dxgiFormat == 71
            Assert.Equal(148 + hdr.Format.MipChainBytes(hdr.Width, hdr.Height, hdr.MipLevels), dds.Length);
        }
        finally { dir.Delete(true); }
    }

    private static string MakePng(string dir, string name, int size, Rgba32 color)
    {
        using var img = new Image<Rgba32>(size, size, color);
        string path = Path.Combine(dir, name + ".png");
        img.SaveAsPng(path);
        return path;
    }
}
