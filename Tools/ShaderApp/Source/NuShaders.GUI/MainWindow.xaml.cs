using System.Windows;
using Microsoft.Win32;
using NuShaders.GUI.ViewModels;

namespace NuShaders.GUI;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
    }

    private void OpenBundle_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog();
        if (dlg.ShowDialog() != true) return;
        try { _vm.Workspace.Open(dlg.FolderName); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Open bundle failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void AddReferenceBundle_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.Workspace.Catalog is null)
        {
            MessageBox.Show(this, "Open a bundle first, then add reference bundles.", "No bundle", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var dlg = new OpenFolderDialog { Multiselect = true };
        if (dlg.ShowDialog() != true) return;
        foreach (string folder in dlg.FolderNames)
        {
            try { _vm.Workspace.AddReferenceBundle(folder); }
            catch (Exception ex) { MessageBox.Show(this, $"{folder}:\n{ex.Message}", "Add reference bundle failed", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
    }
}
