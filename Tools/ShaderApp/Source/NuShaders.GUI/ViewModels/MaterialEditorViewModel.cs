using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using NuShaders.Formats.BPR;
using NuShaders.Formats.Bundle;
using NuShaders.Formats.Model;
using NuShaders.GUI.Common;
using NuShaders.Imaging;

namespace NuShaders.GUI.ViewModels;

/// <summary>
/// Edit an existing material: pick it from the open bundle, edit its shader-named parameters, reassign its textures
/// (TextureState) / render mode (MaterialState) / shader from catalog pick-lists, and Save to a non-destructive output
/// folder. All edits are surgical (param float4 patches + imports re-points); a shader change is an id repoint
/// (incompatible-layout rebuild is the Creator's job).
/// </summary>
public sealed class MaterialEditorViewModel : ViewModelBase
{
    private readonly BundleWorkspaceViewModel _ws;
    private uint? _originalShaderID;

    public BundleWorkspaceViewModel Workspace => _ws;

    /// <summary>The Textures tab (list + preview + rename).</summary>
    public TexturePreviewViewModel TexturePreview { get; }

    public MaterialEditorViewModel(BundleWorkspaceViewModel ws)
    {
        _ws = ws;
        TexturePreview = new TexturePreviewViewModel(ws);
        SaveCommand = new RelayCommand(() => _ = SaveStandaloneAsync(), () => CanSave);
        SaveAsNewCommand = new RelayCommand(() => _ = SaveAsNewAsync(),
            () => CanSave && !string.IsNullOrWhiteSpace(NewMaterialName));
        RenameCommand = new RelayCommand(Rename,
            () => SelectedMaterial is not null && !string.IsNullOrWhiteSpace(RenameText) && !IsBusy);
        _ws.BundleChanged += RebuildMergeTargets;
        RebuildMergeTargets();
    }

    private bool CanSave => SelectedMaterial?.DatPath is not null && !IsBusy;

    private CatalogResource? _selectedMaterial;
    public CatalogResource? SelectedMaterial
    {
        get => _selectedMaterial;
        set
        {
            if (!SetProperty(ref _selectedMaterial, value)) return;
            RenameText = value?.Name is not null ? value.ShortName : "";   // seed the rename box (empty for unnamed)
            LoadMaterial();
        }
    }

    private string _renameText = "";
    public string RenameText { get => _renameText; set { if (SetProperty(ref _renameText, value)) CommandManager.InvalidateRequerySuggested(); } }

    public ObservableCollection<MaterialParamViewModel> Parameters { get; } = [];
    public ObservableCollection<SamplerSlotViewModel> Samplers { get; } = [];
    public ObservableCollection<TechniqueStateViewModel> Techniques { get; } = [];

    /// <summary>The "Merge into…" dropdown targets — one per loaded bundle (open + references).</summary>
    public ObservableCollection<MergeTargetViewModel> MergeTargets { get; } = [];

    private CatalogResource? _selectedShader;
    public CatalogResource? SelectedShader
    {
        get => _selectedShader;
        set { if (SetProperty(ref _selectedShader, value)) RebindForShader(); }
    }
    private bool _suppressShaderRebind;

    private string _shaderStatus = "";
    public string ShaderStatus { get => _shaderStatus; private set => SetProperty(ref _shaderStatus, value); }

    private string _outputFolder = "";
    public string OutputFolder { get => _outputFolder; set => SetProperty(ref _outputFolder, value); }

    private string _newMaterialName = "";
    public string NewMaterialName { get => _newMaterialName; set => SetProperty(ref _newMaterialName, value); }

    private string _log = "";
    public string Log { get => _log; private set => SetProperty(ref _log, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) CommandManager.InvalidateRequerySuggested(); } }

    public RelayCommand SaveCommand { get; }
    public RelayCommand SaveAsNewCommand { get; }
    public RelayCommand RenameCommand { get; }

    /// <summary>Rebuild the "Merge into…" dropdown from the currently-loaded bundles (open bundle first, then references).</summary>
    private void RebuildMergeTargets()
    {
        MergeTargets.Clear();
        foreach (var b in _ws.LoadedBundles)
            MergeTargets.Add(new MergeTargetViewModel(b.Name, () => _ = MergeIntoAsync(b.Folder), () => CanSave));
    }

    /// <summary>Merge the edited material into an arbitrary bundle folder ("Merge into another bundle…").</summary>
    public void MergeInto(string folder) => _ = MergeIntoAsync(folder);

    private void Rename()
    {
        if (SelectedMaterial is not { } mat || string.IsNullOrWhiteSpace(RenameText) || _ws.Catalog is null) return;
        try
        {
            string name = RenameText.Trim();
            var renamed = _ws.RenameResource(mat, name);
            Log = $"Named {mat.Id} → \"{name}\" (saved to .debug.xml).";
            if (renamed is not null) SelectedMaterial = renamed;   // reselect the renamed item
        }
        catch (Exception ex) { Log = "ERROR: " + ex.Message; }
    }

    private void LoadMaterial()
    {
        _suppressShaderRebind = true;   // assigning SelectedShader below must not trigger a re-bind
        try
        {
            Parameters.Clear(); Samplers.Clear(); Techniques.Clear();
            SelectedShader = null; ShaderStatus = "";
            if (_ws.Combined is null || SelectedMaterial is null) return;

            var b = ShaderParameterBinding.Bind(_ws.Combined, SelectedMaterial);
            _originalShaderID = b.ShaderID?.Value;
            SelectedShader = b.ShaderID is { } sid ? _ws.Shaders.FirstOrDefault(s => s.Id.Value == sid.Value) : null;
            ShaderStatus = b.ShaderFound
                ? ""
                : $"{b.ShaderID?.ToString() ?? "(none)"} — NOT IN BUNDLE (parameter names unresolved)";

            PopulateRows(b);
            // Render-mode rows are shader-independent → built here (NOT in PopulateRows, so a shader re-bind won't reset them).
            foreach (var st in b.MaterialStates)
                Techniques.Add(new TechniqueStateViewModel(st.TechniqueIndex, st.SlotOffset, st.MaterialStateID?.Value, _ws));
        }
        finally { _suppressShaderRebind = false; }
    }

    private void PopulateRows(BoundMaterial b)
    {
        Parameters.Clear(); Samplers.Clear();
        foreach (var p in b.Parameters)
            Parameters.Add(new MaterialParamViewModel(p.Name, p.NameHash, p.IsPixel, p.MaterialConstIndex, p.Size, p.CurrentValue, p.ShaderDefault, p.ResolvedInShader));
        foreach (var s in b.Samplers)
            Samplers.Add(new SamplerSlotViewModel(s, _ws));
    }

    /// <summary>Shader changed in the combo: re-bind the param/sampler rows to the NEW shader's surface (carrying values
    /// over). The render-mode (Techniques) rows are shader-independent, so they're left as-is. Save will rebuild.</summary>
    private void RebindForShader()
    {
        if (_suppressShaderRebind || _ws.Combined is null || SelectedMaterial is null || SelectedShader is null) return;
        if (SelectedShader.DatPath is null || !File.Exists(SelectedShader.DatPath))
        {
            ShaderStatus = $"{SelectedShader.ShortName} — shader .dat not in the loaded bundles (can't re-bind)";
            return;
        }
        var shader = BPRShaderResource.Read(File.ReadAllBytes(SelectedShader.DatPath)).Decode();
        var b = ShaderParameterBinding.BindForShader(_ws.Combined, SelectedMaterial, shader, SelectedShader.Id, SelectedShader.Name);
        PopulateRows(b);
        ShaderStatus = SelectedShader.Id.Value != _originalShaderID
            ? "shader changed → material will be REBUILT for it on Save (experimental — verify in-game)"
            : $"resolved: {SelectedShader.ShortName}";
    }

    private sealed record EditResult(ResourceID Id, byte[] Dat, ImportsYAML Imports, IReadOnlyList<EmittedResource> NewTextureStates);

    /// <summary>Apply every edit to a fresh copy of the material + imports, and resolve each sampler's (texture + settings)
    /// to a find-or-created TextureState — collecting any newly built states so the caller writes them with the material.</summary>
    private EditResult BuildEdited()
    {
        // Shader swap → the rows were re-bound to the new shader, so a surgical SetConstant-by-index won't fit; rebuild.
        if (SelectedShader is { } sw && sw.Id.Value != _originalShaderID)
            return BuildRebuilt(sw);

        var mat = BPRMaterialResource.Read(File.ReadAllBytes(SelectedMaterial!.DatPath!));
        var imports = SelectedMaterial.ImportsPath is not null && File.Exists(SelectedMaterial.ImportsPath)
            ? ImportsYAML.Read(File.ReadAllText(SelectedMaterial.ImportsPath))
            : new ImportsYAML();
        var d = mat.Decode();

        foreach (var p in Parameters)
        {
            int count = (p.IsPixel ? d.PSConstants : d.VSConstants).Count;
            if (p.ConstIndex >= 0 && p.ConstIndex < count) mat.SetConstant(p.IsPixel, p.ConstIndex, p.ParseValue());
        }

        var newStates = ResolveSamplers(out var assigns);
        foreach (var a in assigns)
            if (a.TsID.Value != a.OriginalTs) BPRMaterialResource.RepointImport(imports, a.SlotOffset, a.TsID);

        foreach (var t in Techniques)
            if (t.SelectedMaterialState is { } ms && ms.Id.Value != t.OriginalMaterialStateID)
                BPRMaterialResource.RepointImport(imports, t.SlotOffset, ms.Id);
        return new EditResult(SelectedMaterial.Id, mat.ToBytes(), imports, newStates);
    }

    private sealed record SamplerResolve(int Channel, int SlotOffset, uint OriginalTs, ResourceID TsID);

    /// <summary>Resolve every bound sampler's (texture + settings) to a reused/created TextureState id (find-or-create),
    /// returning the per-sampler resolution and any newly created states to write alongside the material.</summary>
    private List<EmittedResource> ResolveSamplers(out List<SamplerResolve> assigns)
    {
        var newStates = new List<EmittedResource>();
        assigns = [];
        foreach (var s in Samplers)
        {
            if (s.TextureID is not { } texID) continue;
            ResourceID tsID;
            var match = _ws.Combined is { } cat ? TextureStateResolver.FindMatch(cat, texID, s.CurrentSampler) : null;
            if (match is not null) tsID = match.Id;
            else { var res = TextureStateResolver.Create(texID, s.CurrentSampler); tsID = res.Id; newStates.Add(res); }
            assigns.Add(new SamplerResolve(s.Channel, s.SlotOffset, s.OriginalTextureStateID ?? 0, tsID));
        }
        return newStates;
    }

    /// <summary>Rebuild the material for a swapped-in shader by cloning a bundle material that already uses it
    /// (engine-safe layout) and writing the user's values into the clone. Throws if no such template exists.</summary>
    private EditResult BuildRebuilt(CatalogResource shader)
    {
        var template = FindTemplate(shader.Id)
            ?? throw new InvalidOperationException(
                $"No material in the loaded bundles uses shader '{shader.ShortName}', so the swap can't be rebuilt. " +
                "Add a bundle that has one (Add Reference Bundle…), or pick a shader an existing material already uses.");

        var newStates = ResolveSamplers(out var assigns);
        var textures = assigns.Select(a => new MaterialTextureAssign(a.Channel, a.TsID)).ToList();

        var values = Parameters.Select(p => new MaterialParamValue(p.NameHash, p.ParseValue())).ToList();
        var states = Techniques.Select(t => t.SelectedMaterialState is { } ms ? (ResourceID?)ms.Id : null).ToList();

        byte[] templateDat = File.ReadAllBytes(template.DatPath!);
        var templateImports = template.ImportsPath is not null && File.Exists(template.ImportsPath)
            ? ImportsYAML.Read(File.ReadAllText(template.ImportsPath)) : new ImportsYAML();
        var (dat, imports) = BPRMaterialBuilder.BuildFromTemplate(templateDat, templateImports, SelectedMaterial!.Id, shader.Id, values, textures, states);
        Log += $"REBUILT for shader '{shader.ShortName}' (cloned template {template.ShortName}) — experimental, verify in-game.\n";
        return new EditResult(SelectedMaterial.Id, dat, imports, newStates);
    }

    /// <summary>A material (other than the one being edited) in the loaded bundles whose shader import is <paramref name="shaderID"/>.</summary>
    private CatalogResource? FindTemplate(ResourceID shaderID)
    {
        if (_ws.Combined is null) return null;
        foreach (var m in _ws.Combined.Materials)
        {
            if (m.DatPath is null || m.ImportsPath is null || !File.Exists(m.ImportsPath)) continue;
            if (m.Id.Value == SelectedMaterial!.Id.Value) continue;
            foreach (var e in ImportsYAML.Read(File.ReadAllText(m.ImportsPath)).Entries)
                if (e.Offset == (uint)BPRMaterialResource.ShaderSlotOffset && e.Id == shaderID.Value)
                    return m;
        }
        return null;
    }

    /// <summary>Non-destructive save: write the edited material (+ any new texture-states) standalone to the output folder.</summary>
    private async Task SaveStandaloneAsync()
    {
        if (_ws.Catalog is null || SelectedMaterial?.DatPath is null) return;
        IsBusy = true; Log = "";
        try
        {
            var r = BuildEdited();
            string? matName = SelectedMaterial?.Name;   // persist the resolved name to .debug.xml on save
            string outDir = string.IsNullOrWhiteSpace(OutputFolder)
                ? Path.Combine(Path.GetTempPath(), "NuShadersMaterials", r.Id.ToHexUpper())
                : OutputFolder;
            await Task.Run(() =>
            {
                Directory.CreateDirectory(outDir);
                MaterialWriter.WriteStandalone(outDir, r.Id, r.Dat, r.Imports, matName);
                if (r.NewTextureStates.Count > 0) TextureSetWriter.MergeIntoBundle(outDir, r.NewTextureStates);
            });
            Log += $"Saved material {r.Id} → {outDir} ({r.NewTextureStates.Count} new texture-state(s))\n(Non-destructive: merge into a bundle to apply.)";
        }
        catch (Exception ex) { Log += "ERROR: " + ex.Message; }
        finally { IsBusy = false; }
    }

    /// <summary>Merge the edited material (+ any new texture-states) into a chosen bundle folder — the open bundle or any
    /// loaded/other bundle (e.g. WORLDTEX). The material id is unchanged; repack the bundle to apply in-game.</summary>
    private async Task MergeIntoAsync(string folder)
    {
        if (_ws.Catalog is null || SelectedMaterial?.DatPath is null || string.IsNullOrWhiteSpace(folder)) return;
        IsBusy = true; Log = "";
        try
        {
            var r = BuildEdited();
            string? matName = SelectedMaterial?.Name;
            await Task.Run(() =>
            {
                if (r.NewTextureStates.Count > 0) TextureSetWriter.MergeIntoBundle(folder, r.NewTextureStates);
                MaterialWriter.MergeIntoBundle(folder, r.Id, r.Dat, r.Imports, matName);
            });
            Log += $"Merged material {r.Id} into {Path.GetFileName(folder.TrimEnd('\\', '/'))} ({r.NewTextureStates.Count} new texture-state(s)).\n  {folder}\n(Repack the bundle to apply in-game.)";
        }
        catch (Exception ex) { Log += "ERROR: " + ex.Message; }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Create a NEW material as a byte-clone of the current (edited) one under a new resource id — the safe creator:
    /// the result is structurally identical to a working material (only its name-hash/id + your edits differ), so it
    /// loads in-game without the de-novo-rebuild risk. The new material reuses the template's shader, so it's a true
    /// new material whenever you start from one that already uses the shader you want.
    /// </summary>
    private async Task SaveAsNewAsync()
    {
        if (_ws.Catalog is null || SelectedMaterial?.DatPath is null || string.IsNullOrWhiteSpace(NewMaterialName)) return;
        IsBusy = true; Log = "";
        try
        {
            var template = SelectedMaterial!;
            var r = BuildEdited();
            string gamedbName = BurnoutResourceName.SynthesizeMaterialName(NewMaterialName.Trim());
            var newID = BurnoutResourceName.ResourceIDFor(gamedbName);
            var clone = BPRMaterialResource.Read(r.Dat);
            clone.SetNameHash(newID.Value);
            byte[] newDat = clone.ToBytes();

            // Immediate model: clone straight into the opened bundle (with any new texture-states), surface it + select it.
            string bundle = _ws.BundleFolder;
            await Task.Run(() =>
            {
                if (r.NewTextureStates.Count > 0) TextureSetWriter.MergeIntoBundle(bundle, r.NewTextureStates);
                MaterialWriter.MergeIntoBundle(bundle, newID, newDat, r.Imports, gamedbName);
            });
            string upper = newID.ToHexUpper();
            var newRes = new CatalogResource(newID, BPRMaterialResource.MetaType, gamedbName,
                Path.Combine(bundle, "Material", $"{upper}.dat"), Path.Combine(bundle, "Material", $"{upper}_imports.yaml"));
            _ws.RegisterMaterial(newRes);
            Log += $"Created material '{NewMaterialName}' = {newID} (byte-clone of {template.Id}) — merged into the bundle.";
            NewMaterialName = "";
            SelectedMaterial = newRes;   // open it for editing
        }
        catch (Exception ex) { Log += "ERROR: " + ex.Message; }
        finally { IsBusy = false; }
    }
}
