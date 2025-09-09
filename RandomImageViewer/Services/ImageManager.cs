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
        private readonly List<CollectionInfo> _allCollections;
        private readonly List<CollectionInfo> _remainingCollections;
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
        public int TotalCollectionCount => _allCollections.Count;
        public int RemainingCollectionCount => _remainingCollections.Count;
        public int TotalEntriesCount => _allImages.Count + _allCollections.Count; // Total selectable entries
        public int RemainingEntriesCount => _remainingImages.Count + _remainingCollections.Count; // Total remaining entries
        public bool HasImages => _allImages.Count > 0 || _allCollections.Count > 0;
        public bool IsInCollection => _currentCollection != null;
        public CollectionInfo CurrentCollection => _currentCollection;

        public ImageManager()
        {
            _allImages = new List<ImageFile>();
            _remainingImages = new List<ImageFile>();
            _allCollections = new List<CollectionInfo>();
            _remainingCollections = new List<CollectionInfo>();
            // Use current time as seed for better randomization between runs
            _random = new Random((int)DateTime.Now.Ticks);
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
            _allCollections.Clear();
            _remainingCollections.Clear();

            await Task.Run(() =>
            {
                try
                {
                    // Get all image files recursively
                    var allFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories)
                        .AsParallel()
                        .Where(file => _supportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                        .ToList();

                    // Group files by directory to identify collections
                    var filesByDirectory = allFiles.GroupBy(file => Path.GetDirectoryName(file)).ToList();

                    int processedCount = 0;
                    bool readyToStartFired = false;
                    const int minEntriesToStart = 20; // Start showing after finding 20 entries (images + collections)
                    const int batchSize = 25; // Smaller batches for more frequent updates
                    const int reshuffleThreshold = 25; // Re-shuffle when we get 25 more entries

                    // Process directories in batches
                    for (int i = 0; i < filesByDirectory.Count; i += batchSize)
                    {
                        var batch = filesByDirectory.Skip(i).Take(batchSize);

                        foreach (var directoryGroup in batch)
                        {
                            var directoryPath = directoryGroup.Key;
                            var filesInDirectory = directoryGroup.ToList();

                            try
                            {
                                // Check if this directory is a collection
                                var collection = _collectionManager.DetectCollection(directoryPath);
                                if (collection != null)
                                {
                                    // This is a collection - add it as a single entry
                                    _collectionManager.PopulateCollectionImages(collection, filesInDirectory.Select(f => new ImageFile(f)).ToList());
                                    _allCollections.Add(collection);
                                    _remainingCollections.Add(collection);
                                }
                                else
                                {
                                    // These are individual images - add each one
                                    foreach (var file in filesInDirectory)
                                    {
                                        var imageFile = new ImageFile(file);
                                        _allImages.Add(imageFile);
                                        _remainingImages.Add(imageFile);
                                    }
                                }

                                // Fire ReadyToStart event when we have enough entries
                                var totalEntries = _allImages.Count + _allCollections.Count;
                                if (!readyToStartFired && totalEntries >= minEntriesToStart)
                                {
                                    // Shuffle both lists
                                    ShuffleList(_remainingImages);
                                    ShuffleList(_remainingCollections);
                                    ReadyToStart?.Invoke(this, EventArgs.Empty);
                                    readyToStartFired = true;
                                }
                                // Re-shuffle periodically for better randomization
                                else if (readyToStartFired && totalEntries % reshuffleThreshold == 0)
                                {
                                    ShuffleList(_remainingImages);
                                    ShuffleList(_remainingCollections);
                                }
                            }
                            catch (Exception ex)
                            {
                                // Log error but continue processing other directories
                                System.Diagnostics.Debug.WriteLine($"Error processing directory {directoryPath}: {ex.Message}");
                            }

                            processedCount += filesInDirectory.Count;
                            ScanProgressChanged?.Invoke(this, processedCount);

                            // Small delay for UI responsiveness on large folders
                            if (processedCount % 100 == 0)
                            {
                                System.Threading.Thread.Sleep(1);
                            }
                        }
                    }

                    // Final shuffle if we didn't start early
                    var finalTotalEntries = _allImages.Count + _allCollections.Count;
                    if (!readyToStartFired && finalTotalEntries > 0)
                    {
                        ShuffleList(_remainingImages);
                        ShuffleList(_remainingCollections);
                        ReadyToStart?.Invoke(this, EventArgs.Empty);
                    }
                    else if (readyToStartFired)
                    {
                        // Final shuffle for better randomness
                        ShuffleList(_remainingImages);
                        ShuffleList(_remainingCollections);
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Error scanning folder: {ex.Message}", ex);
                }
            });

            var totalImages = _allImages.Count;
            var totalCollections = _allCollections.Count;
            ScanCompleted?.Invoke(this, $"Found {totalImages} individual images and {totalCollections} collections");
        }

        /// <summary>
        /// Gets the next image - either from a collection or random selection with equal probability
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

            // Choose between individual images and collections with equal probability
            var totalRemainingEntries = _remainingImages.Count + _remainingCollections.Count;
            if (totalRemainingEntries == 0)
            {
                return null;
            }

            // Randomly choose between individual images and collections
            var randomIndex = _random.Next(totalRemainingEntries);
            
            if (randomIndex < _remainingImages.Count)
            {
                // Select an individual image
                var selectedImage = _remainingImages[randomIndex];
                _remainingImages.RemoveAt(randomIndex);
                return selectedImage;
            }
            else
            {
                // Select a collection
                var collectionIndex = randomIndex - _remainingImages.Count;
                var selectedCollection = _remainingCollections[collectionIndex];
                _remainingCollections.RemoveAt(collectionIndex);
                
                // Set up the collection
                _currentCollection = selectedCollection;
                
                // Fire collection started event
                CollectionStarted?.Invoke(this, selectedCollection);
                
                // Return the first image from the collection
                return _collectionManager.GetNextImageInCollection(selectedCollection);
            }
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
        /// Resets the remaining images and collections lists to include all entries again
        /// </summary>
        public void ResetRemainingImages()
        {
            _remainingImages.Clear();
            _remainingImages.AddRange(_allImages);
            _remainingCollections.Clear();
            _remainingCollections.AddRange(_allCollections);
            ShuffleList(_remainingImages);
            ShuffleList(_remainingCollections);
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
