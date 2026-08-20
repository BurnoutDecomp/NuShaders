using NuShaders.Formats.Model;

namespace NuShaders.Formats.Bundle;

/// <summary>A resource in a bundle: its id, meta type, optional debug name, and on-disk .dat / imports paths.</summary>
public sealed record CatalogResource(ResourceID Id, int MetaType, string? Name, string? DatPath, string? ImportsPath)
{
    /// <summary>Full label: the debug name (gamedb URI) if known, else "0x{id}". Use for tooltips.</summary>
    public string DisplayName => Name ?? ("0x" + Id.ToHexLower());

    /// <summary>
    /// Compact label for lists/combos: the meaningful tail of the gamedb name, e.g.
    /// <c>gamedb://burnout5/Burnout/Content_Vehicles/DefaultMaterial.Material?ID=167850</c> → <c>DefaultMaterial</c>.
    /// Falls back to the hex id when there's no debug name.
    /// </summary>
    public string ShortName
    {
        get
        {
            if (Name is null) return "0x" + Id.ToHexLower();

            string seg = Name;
            int slash = seg.LastIndexOf('/');
            if (slash >= 0) seg = seg[(slash + 1)..];   // drop the gamedb://… path
            int q = seg.IndexOf('?');
            if (q >= 0) seg = seg[..q];                 // drop the ?ID=… query
            int dot = seg.LastIndexOf('.');
            if (dot > 0) seg = seg[..dot];              // drop the trailing .TypeName (Material / TextureConfig2d / …)
            return seg.Length == 0 ? DisplayName : seg;
        }
    }
}

/// <summary>
/// An index of a YAP-extracted bundle folder: the union of its <c>.meta.yaml</c> (id → type, authoritative) and
/// optional <c>.debug.xml</c> (id → gamedb name). Provides per-type pick-lists (Materials, Shaders, MaterialStates,
/// TextureStates, Textures) and id lookup so the material editor can present and re-point references.
/// </summary>
public sealed class BundleCatalog
{
    public required string BundleFolder { get; init; }
    public required IReadOnlyList<CatalogResource> All { get; init; }
    private Dictionary<uint, CatalogResource> _byID = [];

    public IReadOnlyList<CatalogResource> Materials => ByType(0x1);
    public IReadOnlyList<CatalogResource> Shaders => ByType(0x32);
    public IReadOnlyList<CatalogResource> MaterialStates => ByType(0xf);
    public IReadOnlyList<CatalogResource> TextureStates => ByType(0xe);
    public IReadOnlyList<CatalogResource> Textures => ByType(0x0);

    public CatalogResource? ByID(ResourceID id) => _byID.GetValueOrDefault(id.Value);

    private IReadOnlyList<CatalogResource> ByType(int t)
        => All.Where(r => r.MetaType == t).OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();

    /// <summary>Union several catalogs into one (first occurrence of an id wins), for editing a bundle whose
    /// referenced shaders/states live in another bundle. <paramref name="primaryFolder"/> is kept as the folder.</summary>
    public static BundleCatalog Combine(string primaryFolder, IReadOnlyList<BundleCatalog> catalogs)
    {
        var seen = new HashSet<uint>();
        var all = new List<CatalogResource>();
        foreach (var c in catalogs)
            foreach (var r in c.All)
                if (seen.Add(r.Id.Value)) all.Add(r);
        return new BundleCatalog { BundleFolder = primaryFolder, All = all, _byID = all.ToDictionary(r => r.Id.Value) };
    }

    public static BundleCatalog Open(string bundleFolder)
    {
        string metaPath = Path.Combine(bundleFolder, ".meta.yaml");
        if (!File.Exists(metaPath)) throw new FileNotFoundException("bundle has no .meta.yaml", metaPath);
        var meta = MetaYAML.Read(File.ReadAllText(metaPath));
        var debug = DebugXML.TryReadFile(Path.Combine(bundleFolder, ".debug.xml"));

        var all = new List<CatalogResource>();
        foreach (uint id in meta.ResourceIDs)
        {
            int type = meta.TypeOf(id) ?? -1;
            string upper = new ResourceID(id).ToHexUpper();
            string? sub = SubfolderFor(type);
            string? dat = null, imp = null;
            if (sub is not null)
            {
                string datCandidate = Path.Combine(bundleFolder, sub, type == 0x0 ? $"{upper}_primary.dat" : $"{upper}.dat");
                if (File.Exists(datCandidate)) dat = datCandidate;
                string impCandidate = Path.Combine(bundleFolder, sub, $"{upper}_imports.yaml");
                if (File.Exists(impCandidate)) imp = impCandidate;
            }
            string? name = debug?.NameFor(id) ?? ResourceNameDB.Instance.Lookup(id);   // .debug.xml first, then the global ResourceDB
            all.Add(new CatalogResource(new ResourceID(id), type, name, dat, imp));
        }
        return new BundleCatalog { BundleFolder = bundleFolder, All = all, _byID = all.ToDictionary(r => r.Id.Value) };
    }

    private static string? SubfolderFor(int metaType) => metaType switch
    {
        0x1 => "Material",
        0xf => "MaterialState",
        0xe => "TextureState",
        0x0 => "Texture",
        0x32 => "Shader",
        0x12 => "ShaderProgramBuffer",
        _ => null,
    };
}
