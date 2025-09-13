using System;
using System.IO;
using System.Reflection;
using System.Windows;
using RandomImageViewer.Utils;

namespace RandomImageViewer
{
    /// <summary>
    /// About dialog for Random Image Viewer
    /// </summary>
    public partial class AboutDialog : Window
    {
        public AboutDialog()
        {
            InitializeComponent();
            LoadVersionInfo();
            
            // Set icon if it exists
            if (File.Exists("app.ico"))
            {
                this.Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri("app.ico", UriKind.Relative));
            }
        }

        private void LoadVersionInfo()
        {
            var versionInfo = VersionInfo.Data;
            VersionText.Text = $"Version {versionInfo.Version}";
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
