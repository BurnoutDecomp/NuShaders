using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NuShaders.GUI.ViewModels;

namespace NuShaders.GUI.Views;

public partial class TexturePackerView : UserControl
{
    public TexturePackerView() => InitializeComponent();

    private TexturePackerViewModel Vm => (TexturePackerViewModel)DataContext;

    private void BrowseMap_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not MapSlot slot) return;
        var dlg = new OpenFileDialog { Filter = "Images|*.png;*.tga;*.bmp;*.jpg;*.jpeg|All files|*.*" };
        if (dlg.ShowDialog() == true) slot.Path = dlg.FileName;
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog();
        if (dlg.ShowDialog() == true) Vm.OutputFolder = dlg.FolderName;
    }

    private void BrowseBundle_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog();
        if (dlg.ShowDialog() == true) Vm.BundleFolder = dlg.FolderName;
    }
}
