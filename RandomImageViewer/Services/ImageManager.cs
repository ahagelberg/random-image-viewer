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
        private readonly string[] _supportedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private readonly CollectionManager _collectionManager;
        private CollectionInfo _currentCollection;

        public event EventHandler<int> ScanProgressChanged;
        public event EventHandler<string> ScanCompleted;
        public event EventHandler ReadyToStart; // Fired when we have enough images to start showing
        public event EventHandler<CollectionInfo> CollectionStarted; // Fired when a collection is started
        public event EventHandler<CollectionInfo> CollectionCompleted; // Fired when a collection is completed

        public int TotalImageCount => _allImages.Count;
        public int RemainingImageCount => _remainingImages.Count;
        public bool HasImages => _allImages.Count > 0;
        public bool IsInCollection => _currentCollection != null;
        public CollectionInfo CurrentCollection => _currentCollection;

        public ImageManager()
        {
            _allImages = new List<ImageFile>();
            _remainingImages = new List<ImageFile>();
            _random = new Random();
            _collectionManager = new CollectionManager();
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
                    // Optimize for network shares: use parallel processing and batch operations
                    var allFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                        .AsParallel()
                        .Where(file => _supportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                        .ToList();

                    int processedCount = 0;
                    bool readyToStartFired = false;
                    const int minImagesToStart = 10; // Start showing images after finding 10
                    const int batchSize = 50; // Process files in batches for better performance

                    // Process files in batches for better performance on network shares
                    for (int i = 0; i < allFiles.Count; i += batchSize)
                    {
                        var batch = allFiles.Skip(i).Take(batchSize);
                        
                        foreach (var file in batch)
                        {
                            try
                            {
                                var imageFile = new ImageFile(file);
                                _allImages.Add(imageFile);
                                _remainingImages.Add(imageFile);

                                // Fire ReadyToStart event when we have enough images
                                if (!readyToStartFired && _allImages.Count >= minImagesToStart)
                                {
                                    // Shuffle what we have so far
                                    ShuffleList(_remainingImages);
                                    ReadyToStart?.Invoke(this, EventArgs.Empty);
                                    readyToStartFired = true;
                                }
                            }
                            catch (Exception ex)
                            {
                                // Log error but continue processing other files
                                System.Diagnostics.Debug.WriteLine($"Error processing file {file}: {ex.Message}");
                            }

                            processedCount++;
                        }

                        // Report progress after each batch
                        ScanProgressChanged?.Invoke(this, processedCount);
                        
                        // Small delay to prevent UI freezing on large folders
                        if (allFiles.Count > 1000)
                        {
                            System.Threading.Thread.Sleep(1);
                        }
                    }

                    // Final shuffle of all images if we didn't start early
                    if (!readyToStartFired && _remainingImages.Count > 0)
                    {
                        ShuffleList(_remainingImages);
                        ReadyToStart?.Invoke(this, EventArgs.Empty);
                    }
                    else if (readyToStartFired && _remainingImages.Count > minImagesToStart)
                    {
                        // Re-shuffle all images for better randomness
                        ShuffleList(_remainingImages);
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Error scanning folder: {ex.Message}", ex);
                }
            });

            ScanCompleted?.Invoke(this, $"Found {_allImages.Count} images");
        }

        /// <summary>
        /// Gets the next image - either from a collection or random selection
        /// </summary>
        /// <returns>Next image, or null if no images available</returns>
        public ImageFile GetNextImage()
        {
            // If we're in a collection, get the next image from the collection
            if (_currentCollection != null)
            {
                var collectionImage = _collectionManager.GetNextImageInCollection(_currentCollection);
                if (collectionImage != null)
                {
                    return collectionImage;
                }
                else
                {
                    // Collection is complete, fire event and clear it
                    CollectionCompleted?.Invoke(this, _currentCollection);
                    _currentCollection = null;
                }
            }

            // Check if the next random image is from a collection folder
            if (_remainingImages.Count > 0)
            {
                var nextImage = _remainingImages[0];
                var imageFolder = Path.GetDirectoryName(nextImage.FilePath);
                
                // Check if this image's folder is a collection
                var collection = _collectionManager.DetectCollection(imageFolder);
                if (collection != null)
                {
                    // Remove all images from this collection folder from remaining images
                    var collectionImages = _remainingImages.Where(img => 
                        Path.GetDirectoryName(img.FilePath) == imageFolder).ToList();
                    
                    foreach (var img in collectionImages)
                    {
                        _remainingImages.Remove(img);
                    }

                    // Set up the collection
                    _collectionManager.PopulateCollectionImages(collection, collectionImages);
                    _currentCollection = collection;
                    
                    // Fire collection started event
                    CollectionStarted?.Invoke(this, collection);
                    
                    // Return the first image from the collection
                    return _collectionManager.GetNextImageInCollection(collection);
                }
                else
                {
                    // Normal random image
                    _remainingImages.RemoveAt(0);
                    return nextImage;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets the next random image from the remaining images (legacy method)
        /// </summary>
        /// <returns>Next random image, or null if no images available</returns>
        public ImageFile GetNextRandomImage()
        {
            return GetNextImage();
        }

        /// <summary>
        /// Resets the remaining images list to include all images again
        /// </summary>
        public void ResetRemainingImages()
        {
            _remainingImages.Clear();
            _remainingImages.AddRange(_allImages);
            ShuffleList(_remainingImages);
            _currentCollection = null; // Clear any current collection
        }

        /// <summary>
        /// Gets the previous image in the current collection
        /// </summary>
        /// <returns>Previous image in collection, or null if not in collection or at beginning</returns>
        public ImageFile GetPreviousImageInCollection()
        {
            if (_currentCollection == null)
                return null;

            return _collectionManager.GetPreviousImageInCollection(_currentCollection);
        }

        /// <summary>
        /// Checks if there are more images in the current collection
        /// </summary>
        /// <returns>True if there are more images in the collection</returns>
        public bool HasMoreImagesInCollection()
        {
            return _currentCollection != null && _collectionManager.HasMoreImages(_currentCollection);
        }

        /// <summary>
        /// Skips the current collection and returns to random mode
        /// </summary>
        public void SkipCurrentCollection()
        {
            if (_currentCollection != null)
            {
                CollectionCompleted?.Invoke(this, _currentCollection);
                _currentCollection = null;
            }
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
        /// Adds an image back to the remaining images list (for backward navigation)
        /// </summary>
        /// <param name="imageFile">Image to add back</param>
        public void AddImageBack(ImageFile imageFile)
        {
            if (imageFile != null && !_remainingImages.Contains(imageFile))
            {
                _remainingImages.Add(imageFile);
            }
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
