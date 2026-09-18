using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Sms.Common;
using Sms.WpfApp;

namespace Sms.Tests;

[Collection("Console")]
public sealed class WpfLayoutTests
{
    [Fact]
    public void BuildsStyledWindowWithConfiguredRowsAndRendersPreview()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            var temp = Path.Combine(Path.GetTempPath(), "sms-wpf-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                // Do not start the production App: this test must never touch real environment variables.
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.Resources["Ink"] = new SolidColorBrush(Color.FromRgb(54, 54, 54));
                var store = new FakeEnvironmentStore();
                var vm = new EnvironmentEditorViewModel(store, new DailyLog(temp, "test"));
                vm.Load(new EditorOptions
                {
                    EnvironmentVariables = ["SMS_DEMO_SERVER", "SMS_DEMO_TERMINAL", "SMS_DEMO_DESCRIPTION"],
                    DefaultValues = new()
                    {
                        ["SMS_DEMO_SERVER"] = "http://localhost:5080/api", ["SMS_DEMO_TERMINAL"] = "Касса 01",
                        ["SMS_DEMO_DESCRIPTION"] = "Тестовое рабочее место SmartMealService"
                    },
                    Comments = new()
                    {
                        ["SMS_DEMO_SERVER"] = "Адрес сервера", ["SMS_DEMO_TERMINAL"] = "Название рабочего места",
                        ["SMS_DEMO_DESCRIPTION"] = "Произвольный текст; поддерживается несколько строк"
                    }
                });
                var window = new MainWindow(vm)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -20000, Top = -20000, ShowActivated = false, ShowInTaskbar = false
                };
                window.Show();
                var content = (FrameworkElement)window.Content;
                content.Measure(new Size(1288, 650));
                content.Arrange(new Rect(0, 0, 1288, 650));
                content.UpdateLayout();
                content.Dispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
                content.UpdateLayout();
                var grid = (DataGrid)window.FindName("VariablesGrid");
                Assert.Equal(3, grid.Items.Count);
                Assert.Equal(new[] { "Поле", "Значение", "Комментарий" }, grid.Columns.Select(c => (string)c.Header));
                var bitmap = new RenderTargetBitmap(1288, 650, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(content);
                var root = new DirectoryInfo(AppContext.BaseDirectory);
                while (root is not null && !File.Exists(Path.Combine(root.FullName, "SmartMealService.sln"))) root = root.Parent;
                var directory = Path.Combine(root?.FullName ?? temp, "artifacts");
                Directory.CreateDirectory(directory);
                using var file = File.Create(Path.Combine(directory, "wpf-preview.png"));
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(file);

                // Exercise the real DataGrid commit event, not just the view model in isolation.
                grid.CurrentCell = new DataGridCellInfo(vm.Rows[0], grid.Columns[1]);
                Assert.True(grid.BeginEdit());
                grid.UpdateLayout();
                var editor = FindChild<TextBox>(grid);
                Assert.NotNull(editor);
                editor.Text = "http://localhost:6000/api";
                Assert.True(grid.CommitEdit(DataGridEditingUnit.Cell, true));
                Assert.Equal("http://localhost:6000/api", store.Values["SMS_DEMO_SERVER"]);

                grid.CurrentCell = new DataGridCellInfo(vm.Rows[1], grid.Columns[1]);
                Assert.True(grid.BeginEdit());
                grid.UpdateLayout();
                FindChild<TextBox>(grid)!.Text = new string('x', 32767);
                Assert.False(grid.CommitEdit(DataGridEditingUnit.Cell, true));
                Assert.Equal("Касса 01", store.Values["SMS_DEMO_TERMINAL"]);
                grid.CancelEdit(DataGridEditingUnit.Cell);
                window.Close();
                app.Shutdown();
            }
            catch (Exception ex) { failure = ex; }
            finally { if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true); }
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "WPF layout timed out.");
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent is T match) return match;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            if (FindChild<T>(VisualTreeHelper.GetChild(parent, i)) is { } child) return child;
        return null;
    }
}
