using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RandomImageViewer.Models;

namespace RandomImageViewer.Services
{
    /// <summary>
    /// Handles image loading, scaling, and display operations
    /// </summary>
    public class DisplayEngine
    {
        private BitmapSource _currentImage;
        private readonly object _imageLock = new object();

        public event EventHandler<Exception> ImageLoadError;

        /// <summary>
        /// Loads an image from file path
        /// </summary>
        /// <param name="imageFile">Image file to load</param>
        /// <returns>Loaded BitmapSource or null if failed</returns>
        public BitmapSource LoadImage(ImageFile imageFile)
        {
            if (imageFile == null || !File.Exists(imageFile.FilePath))
            {
                return null;
            }

            try
            {
                lock (_imageLock)
                {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(imageFile.FilePath);
                bitmap.EndInit();
                bitmap.Freeze(); // Make it thread-safe

                    _currentImage = bitmap;
                    return _currentImage;
                }
            }
            catch (Exception ex)
            {
                ImageLoadError?.Invoke(this, ex);
                return null;
            }
        }

        /// <summary>
        /// Scales an image to fit within specified dimensions while maintaining aspect ratio
        /// </summary>
        /// <param name="source">Source image</param>
        /// <param name="maxWidth">Maximum width</param>
        /// <param name="maxHeight">Maximum height</param>
        /// <returns>Scaled image</returns>
        public BitmapSource ScaleImageToFit(BitmapSource source, double maxWidth, double maxHeight)
        {
            if (source == null)
                return null;

            double scaleX = maxWidth / source.PixelWidth;
            double scaleY = maxHeight / source.PixelHeight;
            double scale = Math.Min(scaleX, scaleY);

            // Don't upscale if image is smaller than target
            if (scale > 1.0)
                scale = 1.0;

            return ScaleImage(source, scale);
        }

        /// <summary>
        /// Scales an image by a specific factor
        /// </summary>
        /// <param name="source">Source image</param>
        /// <param name="scale">Scale factor</param>
        /// <returns>Scaled image</returns>
        public BitmapSource ScaleImage(BitmapSource source, double scale)
        {
            if (source == null || scale <= 0)
                return source;

            var transform = new ScaleTransform(scale, scale);
            var scaledBitmap = new TransformedBitmap(source, transform);
            scaledBitmap.Freeze();

            return scaledBitmap;
        }

        /// <summary>
        /// Gets the current loaded image
        /// </summary>
        /// <returns>Current image or null</returns>
        public BitmapSource GetCurrentImage()
        {
            lock (_imageLock)
            {
                return _currentImage;
            }
        }

        /// <summary>
        /// Disposes the current image to free memory
        /// </summary>
        public void DisposeCurrentImage()
        {
            lock (_imageLock)
            {
                if (_currentImage != null)
                {
                    // Force garbage collection of the image
                    _currentImage = null;
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }
        }

        /// <summary>
        /// Calculates the optimal size for displaying an image within given constraints
        /// </summary>
        /// <param name="imageSize">Original image size</param>
        /// <param name="containerSize">Available container size</param>
        /// <param name="maintainAspectRatio">Whether to maintain aspect ratio</param>
        /// <returns>Optimal display size</returns>
        public Size CalculateOptimalSize(Size imageSize, Size containerSize, bool maintainAspectRatio = true)
        {
            if (imageSize.Width <= 0 || imageSize.Height <= 0)
                return containerSize;

            if (!maintainAspectRatio)
                return containerSize;

            double scaleX = containerSize.Width / imageSize.Width;
            double scaleY = containerSize.Height / imageSize.Height;
            double scale = Math.Min(scaleX, scaleY);

            // Don't upscale
            if (scale > 1.0)
                scale = 1.0;

            return new Size(imageSize.Width * scale, imageSize.Height * scale);
        }

        /// <summary>
        /// Gets image information for display
        /// </summary>
        /// <param name="imageFile">Image file</param>
        /// <param name="bitmapSource">Loaded bitmap</param>
        /// <returns>Formatted information string</returns>
        public string GetImageInfo(ImageFile imageFile, BitmapSource bitmapSource)
        {
            if (imageFile == null)
                return "No image loaded";

            var info = $"{imageFile.FileName}";
            
            if (bitmapSource != null)
            {
                info += $" ({bitmapSource.PixelWidth} × {bitmapSource.PixelHeight})";
            }

            info += $" - {FormatFileSize(imageFile.FileSize)}";

            return info;
        }

        /// <summary>
        /// Formats file size in human-readable format
        /// </summary>
        /// <param name="bytes">File size in bytes</param>
        /// <returns>Formatted size string</returns>
        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}
