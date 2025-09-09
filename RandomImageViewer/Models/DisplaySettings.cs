using System.Windows;

namespace RandomImageViewer.Models
{
    /// <summary>
    /// Settings for image display behavior
    /// </summary>
    public class DisplaySettings
    {
        public bool IsFullscreen { get; set; } = false;
        public WindowState WindowState { get; set; } = WindowState.Normal;
        public Size WindowSize { get; set; } = new Size(1024, 768);
        public Point WindowLocation { get; set; } = new Point(100, 100);
        public bool FitToScreen { get; set; } = true;
        public bool MaintainAspectRatio { get; set; } = true;
        public double ZoomLevel { get; set; } = 1.0;
        public string LastSelectedFolder { get; set; } = string.Empty;
    }
}
