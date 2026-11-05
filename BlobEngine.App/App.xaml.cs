using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace BlobEngine;

public partial class App : Application
{
    private void OnStartup(object sender, StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            Log("Dispatcher exception", args.Exception);
            MessageBox.Show("BlobEngine обнаружил ошибку. Подробности сохранены в BlobEngine-error.log рядом с программой.\n\n" + args.Exception.Message,
                "Ошибка BlobEngine", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log("Unhandled exception", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log("Task exception", args.Exception);
            args.SetObserved();
        };

        try
        {
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            Log("Startup exception", ex);
            MessageBox.Show("Не удалось запустить BlobEngine. Подробности сохранены в BlobEngine-error.log.\n\n" + ex,
                "Ошибка запуска", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private static void Log(string title, Exception? exception)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BlobEngine");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "BlobEngine-error.log");
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {title}\n{exception}\n\n");
        }
        catch { }
    }
}