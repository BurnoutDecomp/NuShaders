using System.IO;
using NuShaders.Formats.BPR;
using NuShaders.Formats.Bundle;
using NuShaders.GUI.Common;

namespace NuShaders.GUI.ViewModels;

/// <summary>One technique's render-mode row: a MaterialState pick-list bound to the technique's import slot, with a decoded Opaque/Transparent label.</summary>
public sealed class TechniqueStateViewModel : ViewModelBase
{
    public int TechniqueIndex { get; }
    public int SlotOffset { get; }
    public uint? OriginalMaterialStateID { get; }
    public IReadOnlyList<CatalogResource> MaterialStateChoices { get; }

    private CatalogResource? _selected;
    public CatalogResource? SelectedMaterialState
    {
        get => _selected;
        set { if (SetProperty(ref _selected, value)) UpdateLabel(); }
    }

    private string _renderModeLabel = "";
    public string RenderModeLabel { get => _renderModeLabel; private set => SetProperty(ref _renderModeLabel, value); }

    public TechniqueStateViewModel(int techniqueIndex, int slotOffset, uint? originalID, BundleWorkspaceViewModel ws)
    {
        TechniqueIndex = techniqueIndex;
        SlotOffset = slotOffset;
        OriginalMaterialStateID = originalID;
        MaterialStateChoices = ws.MaterialStates;
        _selected = originalID is { } id ? ws.MaterialStates.FirstOrDefault(r => r.Id.Value == id) : null;
        UpdateLabel();
    }

    private void UpdateLabel()
    {
        RenderModeLabel = "";
        if (_selected?.DatPath is { } p && File.Exists(p))
        {
            var info = MaterialStateDecoder.Decode(File.ReadAllBytes(p));
            if (info is not null) RenderModeLabel = info.Label;
        }
    }
}
