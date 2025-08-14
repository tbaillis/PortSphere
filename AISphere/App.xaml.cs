using System.Windows;
using System.Windows.Threading;

namespace AISphere
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            try
            {
                var win = new MainWindow();
                win.Show();
            }
            catch (System.Exception ex)
            {
                System.Console.Error.WriteLine($"Startup exception: {ex}");
                MessageBox.Show(ex.ToString(), "Startup exception", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(-1);
            }
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            // Log to console and show a dialog to aid troubleshooting
            System.Console.Error.WriteLine($"Unhandled exception: {e.Exception}");
            MessageBox.Show(
                e.Exception.ToString(),
                "Unhandled exception",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
