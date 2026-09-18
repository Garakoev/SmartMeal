using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Sms.Common;

namespace Sms.WpfApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DailyLog? log = null;
        try
        {
            using var configuration = Settings.Load();
            log = new DailyLog(Path.GetFullPath(configuration["LogDirectory"] ?? "logs", AppContext.BaseDirectory), "test-sms-wpf-app");
            var viewModel = new EnvironmentEditorViewModel(new UserEnvironmentStore(), log);
            viewModel.Load(configuration.Get<EditorOptions>() ?? new EditorOptions());
            MainWindow = new MainWindow(viewModel);
            MainWindow.Show();
        }
        catch (Exception ex)
        {
            try
            {
                log ??= new DailyLog(Path.Combine(AppContext.BaseDirectory, "logs"), "test-sms-wpf-app");
                log.Event("StartupFailed", new { ex.Message });
            }
            catch (Exception) { /* The startup error is still visible if the log directory is inaccessible. */ }
            MessageBox.Show(ex.Message, "Не удалось запустить редактор", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
