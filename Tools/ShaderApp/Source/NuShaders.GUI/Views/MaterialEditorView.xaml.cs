using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using NuShaders.Formats.Bundle;
using NuShaders.GUI.ViewModels;

namespace NuShaders.GUI.Views;

public partial class MaterialEditorView : UserControl
{
    public MaterialEditorView() => InitializeComponent();

    private void ResetParam_Click(object sender, RoutedEventArgs e)
        => (((FrameworkElement)sender).DataContext as MaterialParamViewModel)?.ResetToDefault();

    private void Swatch_Click(object sender, MouseButtonEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not MaterialParamViewModel p) return;
        var rgba = p.GetRGBA();
        var vm = new ColorPickerViewModel(rgba[0], rgba[1], rgba[2], rgba[3]);
        var dlg = new ColorPickerDialog(vm) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true && vm.HasChanged) p.SetRGBA(vm.Result);   // no-op open+OK leaves ValueText exactly as-is
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog();
        if (dlg.ShowDialog() == true && DataContext is MaterialEditorViewModel vm)
            vm.OutputFolder = dlg.FolderName;
    }

    private void MergeIntoOther_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MaterialEditorViewModel vm) return;
        var dlg = new OpenFolderDialog { Title = "Pick a bundle folder to merge the material into" };
        if (dlg.ShowDialog() == true) vm.MergeInto(dlg.FolderName);
    }

    private void CreateTexture_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not SamplerSlotViewModel slot) return;
        if (DataContext is not MaterialEditorViewModel vm || vm.Workspace.Catalog is null) return;

        string nameBase = vm.SelectedMaterial?.Id.ToHexUpper() ?? "Material";
        var dlgVm = new CreateTextureViewModel(vm.Workspace, slot.Kind, nameBase);
        var dlg = new CreateTextureDialog(dlgVm) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true && dlgVm.ResultTexture is { } tex)
        {
            vm.Workspace.RegisterTexture(tex);
            slot.SetTexture(tex);   // bind the new texture; its TextureState is found-or-created on save
        }
    }

    private void BrowseTexture_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not SamplerSlotViewModel slot) return;
        if (DataContext is not MaterialEditorViewModel vm || vm.Workspace.Catalog is null) return;

        var dlgVm = new TextureBrowserViewModel(vm.Workspace.Textures);
        var dlg = new TextureBrowserDialog(dlgVm) { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true && dlgVm.Result is { } tex) slot.SetTexture(tex);
    }

    private void ExportDDSTexture_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is CatalogResource res)
            TextureExport.SaveAsDDS(res, Window.GetWindow(this));
    }

    private void ExportDDSSampler_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is SamplerSlotViewModel { TextureID: { } tid }
            && DataContext is MaterialEditorViewModel vm)
            TextureExport.SaveAsDDS(vm.Workspace.Combined?.ByID(tid), Window.GetWindow(this));
    }
}
