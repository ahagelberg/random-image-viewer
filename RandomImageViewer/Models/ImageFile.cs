using System;
using System.IO;

namespace RandomImageViewer.Models
{
    /// <summary>
    /// Represents an image file with metadata
    /// </summary>
    public class ImageFile
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string Directory { get; set; }
        public long FileSize { get; set; }
        public DateTime LastModified { get; set; }
        public ImageFormat Format { get; set; }

        public ImageFile(string filePath)
        {
            FilePath = filePath;
            FileName = Path.GetFileName(filePath);
            Directory = Path.GetDirectoryName(filePath);
            
            var fileInfo = new FileInfo(filePath);
            FileSize = fileInfo.Length;
            LastModified = fileInfo.LastWriteTime;
            Format = GetImageFormat(Path.GetExtension(filePath).ToLowerInvariant());
        }

        private static ImageFormat GetImageFormat(string extension)
        {
            return extension switch
            {
                ".jpg" or ".jpeg" => ImageFormat.JPEG,
                ".png" => ImageFormat.PNG,
                ".gif" => ImageFormat.GIF,
                ".bmp" => ImageFormat.BMP,
                ".tiff" or ".tif" => ImageFormat.TIFF,
                ".webp" => ImageFormat.WebP,
                _ => ImageFormat.Unknown
            };
        }

        public override string ToString()
        {
            return $"{FileName} ({Format})";
        }

        public override bool Equals(object obj)
        {
            if (obj is ImageFile other)
            {
                return FilePath.Equals(other.FilePath, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return FilePath.GetHashCode(StringComparison.OrdinalIgnoreCase);
        }
    }

    public enum ImageFormat
    {
        Unknown,
        JPEG,
        PNG,
        GIF,
        BMP,
        TIFF,
        WebP
    }
}
