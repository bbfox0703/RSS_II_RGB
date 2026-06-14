using Avalonia.Controls;
using Avalonia.Interactivity;

namespace RSS_II_RGB.App;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void OnOpenZoneEditor(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            var editor = new ZoneEditorWindow { DataContext = new ZoneEditorViewModel(vm.Controller, vm.Settings) };
            editor.Show(this);
        }
    }
}
