using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Sms.WpfApp;

public partial class MainWindow : Window
{
    private readonly EnvironmentEditorViewModel _viewModel;
    public MainWindow(EnvironmentEditorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void OnTitleDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed
            && e.OriginalSource is not System.Windows.Shapes.Shape)
            DragMove();
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnClose(object sender, RoutedEventArgs e) => Close();
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!VariablesGrid.CommitEdit(DataGridEditingUnit.Cell, true)) e.Cancel = true;
    }

    private void OnTableSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ((FrameworkElement)sender).Clip = new RectangleGeometry(new Rect(e.NewSize), 32, 32);
    }

    private void OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit || e.Row.Item is not EnvironmentRow row) return;
        var editor = FindChild<TextBox>(e.EditingElement);
        var before = row.Value;
        if (editor is not null && !_viewModel.Save(row, editor.Text))
        {
            e.Cancel = true;
            if (row.Value != before) editor.Text = row.Value;
        }
    }

    private void OnGridKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            VariablesGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            e.Handled = true;
        }
    }

    private void OnEditorLoaded(object sender, RoutedEventArgs e)
    {
        var editor = (TextBox)sender;
        editor.Focus();
        editor.CaretIndex = editor.Text.Length;
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent is T match) return match;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            if (FindChild<T>(VisualTreeHelper.GetChild(parent, index)) is { } child) return child;
        return null;
    }
}
