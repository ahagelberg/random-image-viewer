using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using RandomImageViewer.Models;

namespace RandomImageViewer.Services
{
    /// <summary>
    /// Manages image file discovery, indexing, and random selection
    /// </summary>
    public class ImageManager
    {
        private readonly List<ImageFile> _allImages;
        private readonly List<ImageFile> _remainingImages;
        private readonly Random _random;
        private readonly string[] _supportedExtensions = { ".jpg", ".jpeg", ".png" };

        public event EventHandler<int> ScanProgressChanged;
        public event EventHandler<string> ScanCompleted;

        public int TotalImageCount => _allImages.Count;
        public int RemainingImageCount => _remainingImages.Count;
        public bool HasImages => _allImages.Count > 0;

        public ImageManager()
        {
            _allImages = new List<ImageFile>();
            _remainingImages = new List<ImageFile>();
            _random = new Random();
        }

        /// <summary>
        /// Scans a folder and all subfolders for supported image files
        /// </summary>
        /// <param name="folderPath">Path to the folder to scan</param>
        /// <returns>Task representing the scan operation</returns>
        public async Task ScanFolderAsync(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException($"Folder not found: {folderPath}");
            }

            _allImages.Clear();
            _remainingImages.Clear();

            await Task.Run(() =>
            {
                try
                {
                    var allFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                        .Where(file => _supportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                        .ToList();

                    int processedCount = 0;
                    foreach (var file in allFiles)
                    {
                        try
                        {
                            var imageFile = new ImageFile(file);
                            _allImages.Add(imageFile);
                            _remainingImages.Add(imageFile);
                        }
                        catch (Exception ex)
                        {
                            // Log error but continue processing other files
                            System.Diagnostics.Debug.WriteLine($"Error processing file {file}: {ex.Message}");
                        }

                        processedCount++;
                        if (processedCount % 10 == 0) // Report progress every 10 files
                        {
                            ScanProgressChanged?.Invoke(this, processedCount);
                        }
                    }

                    // Shuffle the remaining images for random order
                    ShuffleList(_remainingImages);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Error scanning folder: {ex.Message}", ex);
                }
            });

            ScanCompleted?.Invoke(this, $"Found {_allImages.Count} images");
        }

        /// <summary>
        /// Gets the next random image from the remaining images
        /// </summary>
        /// <returns>Next random image, or null if no images available</returns>
        public ImageFile GetNextRandomImage()
        {
            if (_remainingImages.Count == 0)
            {
                return null;
            }

            var selectedImage = _remainingImages[0];
            _remainingImages.RemoveAt(0);

            return selectedImage;
        }

        /// <summary>
        /// Resets the remaining images list to include all images again
        /// </summary>
        public void ResetRemainingImages()
        {
            _remainingImages.Clear();
            _remainingImages.AddRange(_allImages);
            ShuffleList(_remainingImages);
        }

        /// <summary>
        /// Gets all discovered images
        /// </summary>
        /// <returns>List of all image files</returns>
        public List<ImageFile> GetAllImages()
        {
            return new List<ImageFile>(_allImages);
        }

        /// <summary>
        /// Removes an image from both lists (when deleted)
        /// </summary>
        /// <param name="imageFile">Image to remove</param>
        public void RemoveImage(ImageFile imageFile)
        {
            _allImages.Remove(imageFile);
            _remainingImages.Remove(imageFile);
        }

        /// <summary>
        /// Shuffles a list using Fisher-Yates algorithm
        /// </summary>
        /// <param name="list">List to shuffle</param>
        private void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        /// Gets statistics about the image collection
        /// </summary>
        /// <returns>Dictionary with statistics</returns>
        public Dictionary<string, object> GetStatistics()
        {
            var stats = new Dictionary<string, object>
            {
                ["TotalImages"] = _allImages.Count,
                ["RemainingImages"] = _remainingImages.Count,
                ["TotalSize"] = _allImages.Sum(img => img.FileSize),
                ["Formats"] = _allImages.GroupBy(img => img.Format)
                    .ToDictionary(g => g.Key.ToString(), g => g.Count())
            };

            return stats;
        }
    }
}
