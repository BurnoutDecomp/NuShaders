using System.Collections.ObjectModel;
using System.IO;
using NuShaders.Formats.Bundle;
using NuShaders.GUI.Common;

namespace NuShaders.GUI.ViewModels;

/// <summary>A write target: a loaded bundle's display name + its folder on disk (the open bundle or any reference bundle).</summary>
public sealed record BundleTarget(string Name, string Folder);

/// <summary>
/// The shared workspace: a PRIMARY bundle (the materials being edited + the save/merge target) plus optional REFERENCE
/// bundles (e.g. a shaders bundle) whose resources augment the pick-lists and the parameter-name resolution. Materials
/// come from the primary; shaders / material-states / texture-states are the union across all opened bundles.
/// </summary>
public sealed class BundleWorkspaceViewModel : ViewModelBase
{
    private BundleCatalog? _primary;
    private readonly List<BundleCatalog> _references = [];

    /// <summary>The primary (materials) bundle — also the save/merge target.</summary>
    public BundleCatalog? Catalog => _primary;
    /// <summary>The union of the primary + reference bundles — used for ref/shader resolution.</summary>
    public BundleCatalog? Combined { get; private set; }

    private string _bundleFolder = "";
    public string BundleFolder { get => _bundleFolder; private set => SetProperty(ref _bundleFolder, value); }

    private string _referenceBundles = "";
    public string ReferenceBundles { get => _referenceBundles; private set => SetProperty(ref _referenceBundles, value); }

    public ObservableCollection<CatalogResource> Materials { get; } = [];
    public ObservableCollection<CatalogResource> Shaders { get; } = [];
    public ObservableCollection<CatalogResource> MaterialStates { get; } = [];
    public ObservableCollection<CatalogResource> TextureStates { get; } = [];
    public ObservableCollection<CatalogResource> Textures { get; } = [];

    /// <summary>Every loaded bundle as a write target — the open bundle first, then each reference (e.g. a WORLDTEX bundle).</summary>
    public IReadOnlyList<BundleTarget> LoadedBundles
    {
        get
        {
            var list = new List<BundleTarget>();
            if (_primary is not null) list.Add(new BundleTarget($"(open) {NameOf(_primary.BundleFolder)}", _primary.BundleFolder));
            foreach (var r in _references) list.Add(new BundleTarget(NameOf(r.BundleFolder), r.BundleFolder));
            return list;
        }
    }

    private static string NameOf(string folder) => Path.GetFileName(folder.TrimEnd('\\', '/'));

    public event Action? BundleChanged;

    public void Open(string folder)
    {
        _primary = BundleCatalog.Open(folder);
        BundleFolder = folder;
        Rebuild();
    }

    /// <summary>Add a reference bundle (e.g. the SHADERS bundle) so its shaders/states feed the pick-lists + name resolution.</summary>
    public void AddReferenceBundle(string folder)
    {
        _references.Add(BundleCatalog.Open(folder));
        ReferenceBundles = string.Join("; ", _references.Select(r => Path.GetFileName(r.BundleFolder.TrimEnd('\\', '/'))));
        Rebuild();
    }

    private void Rebuild()
    {
        if (_primary is null) return;
        var cats = new List<BundleCatalog> { _primary };
        cats.AddRange(_references);
        Combined = BundleCatalog.Combine(_primary.BundleFolder, cats);
        Fill(Materials, _primary.Materials);
        Fill(Shaders, Combined.Shaders);
        Fill(MaterialStates, Combined.MaterialStates);
        Fill(TextureStates, Combined.TextureStates);
        Fill(Textures, Combined.Textures);
        BundleChanged?.Invoke();
    }

    private static void Fill(ObservableCollection<CatalogResource> dst, IReadOnlyList<CatalogResource> src)
    {
        dst.Clear();
        foreach (var r in src) dst.Add(r);
    }

    /// <summary>Add a just-merged TextureState to the pick-list without a full reload (preserves in-progress edits).</summary>
    public void RegisterTextureState(CatalogResource ts)
    {
        if (TextureStates.All(r => r.Id.Value != ts.Id.Value)) TextureStates.Add(ts);
    }

    /// <summary>Add a just-created material to the list without a full reload.</summary>
    public void RegisterMaterial(CatalogResource mat)
    {
        if (Materials.All(r => r.Id.Value != mat.Id.Value)) Materials.Add(mat);
    }

    /// <summary>Add a just-merged texture to the browser list without a full reload.</summary>
    public void RegisterTexture(CatalogResource tex)
    {
        if (Textures.All(r => r.Id.Value != tex.Id.Value)) Textures.Add(tex);
    }

    /// <summary>Assign a friendly name to a resource: persist it to that resource's OWN bundle's .debug.xml (so names on
    /// textures from reference bundles like WORLDTEX persist with them) and update the in-memory lists.</summary>
    public CatalogResource? RenameResource(CatalogResource res, string newName)
    {
        string? bundleFolder = BundleFolderOf(res);
        if (bundleFolder is null) return null;
        DebugXML.UpsertName(bundleFolder, res.Id, DebugXML.TypeString(res.MetaType), newName);
        var renamed = res with { Name = newName };
        foreach (var col in new[] { Materials, Textures, Shaders, MaterialStates, TextureStates })
        {
            int i = col.IndexOf(res);
            if (i >= 0) col[i] = renamed;
        }
        return renamed;
    }

    /// <summary>The bundle folder a resource lives in, derived from its .dat path (`&lt;bundle&gt;/&lt;Type&gt;/{ID}…dat`); falls back to the primary bundle.</summary>
    private string? BundleFolderOf(CatalogResource res)
    {
        if (res.DatPath is { } dat && Path.GetDirectoryName(dat) is { } typeDir && Path.GetDirectoryName(typeDir) is { } bundle && Directory.Exists(bundle))
            return bundle;
        return _primary?.BundleFolder;
    }
}
