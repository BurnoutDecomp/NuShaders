using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using NuShaders.GUI.ViewModels;

namespace NuShaders.GUI.Views;

public partial class CreateTextureDialog : Window
{
    public CreateTextureDialog(CreateTextureViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.CloseRequested += ok => { DialogResult = ok; Close(); };
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not MapSlot slot) return;
        var dlg = new OpenFileDialog { Filter = "Images|*.png;*.tga;*.jpg;*.jpeg;*.bmp;*.dds|All files|*.*" };
        if (dlg.ShowDialog() == true)
        {
            slot.Path = dlg.FileName;
            (DataContext as CreateTextureViewModel)?.SuggestNameFromFile(dlg.FileName);
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
