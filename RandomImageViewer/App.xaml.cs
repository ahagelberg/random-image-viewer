using System.Windows;

namespace RandomImageViewer
{
    /// <summary>
    /// Application entry point
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Set up global exception handling
            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"An unexpected error occurred: {e.Exception.Message}", 
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            
            // Mark as handled to prevent application crash
            e.Handled = true;
        }
    }
}
