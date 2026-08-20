using System.Windows;
using NuShaders.GUI.ViewModels;

namespace NuShaders.GUI.Views;

public partial class ColorPickerDialog : Window
{
    public ColorPickerDialog(ColorPickerViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.CloseRequested += ok => { DialogResult = ok; Close(); };
    }
}
