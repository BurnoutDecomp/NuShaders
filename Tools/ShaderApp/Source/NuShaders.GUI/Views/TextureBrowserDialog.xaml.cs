using System.Windows;
using System.Windows.Input;
using NuShaders.GUI.ViewModels;

namespace NuShaders.GUI.Views;

public partial class TextureBrowserDialog : Window
{
    public TextureBrowserDialog(TextureBrowserViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.CloseRequested += ok => { DialogResult = ok; Close(); };
    }

    private void Item_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is TextureBrowserViewModel { SelectedItem: not null }) { DialogResult = true; Close(); }
    }

    private void ExportDDS_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is TextureBrowserItem it)
            TextureExport.SaveAsDDS(it.Resource, this);
    }
}
