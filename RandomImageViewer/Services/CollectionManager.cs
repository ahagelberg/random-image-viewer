using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using RandomImageViewer.Models;

namespace RandomImageViewer.Services
{
    /// <summary>
    /// Manages detection and handling of special image collection folders
    /// </summary>
    public class CollectionManager
    {
        private readonly string[] _collectionNamingPrefixes = { "[COLLECTION]", "[ALBUM]", "[SEQUENCE]" };
        private readonly string[] _collectionNamingSuffixes = { "_collection", "_album", "_sequence" };

        /// <summary>
        /// Detects if a folder is a special collection using all detection methods
        /// </summary>
        /// <param name="folderPath">Path to the folder to check</param>
        /// <returns>CollectionInfo if it's a collection, null otherwise</returns>
        public CollectionInfo DetectCollection(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
                return null;

            // Method 1: Check for .collection file
            var collectionFile = Path.Combine(folderPath, ".collection");
            if (File.Exists(collectionFile))
            {
                return LoadCollectionFromFile(folderPath, collectionFile);
            }

            // Method 2: Check if folder is inside _collections directory
            if (IsInCollectionsFolder(folderPath))
            {
                return CreateCollectionFromFolder(folderPath, CollectionDetectionMethod.CollectionsFolder);
            }

            // Method 3: Check for special naming convention
            var folderName = Path.GetFileName(folderPath);
            if (HasSpecialNaming(folderName))
            {
                return CreateCollectionFromFolder(folderPath, CollectionDetectionMethod.SpecialNaming);
            }

            return null;
        }

        /// <summary>
        /// Loads collection information from a .collection file
        /// </summary>
        /// <param name="folderPath">Path to the folder</param>
        /// <param name="collectionFilePath">Path to the .collection file</param>
        /// <returns>CollectionInfo object</returns>
        private CollectionInfo LoadCollectionFromFile(string folderPath, string collectionFilePath)
        {
            try
            {
                var collectionInfo = new CollectionInfo
                {
                    FolderPath = folderPath,
                    Name = Path.GetFileName(folderPath),
                    DetectionMethod = CollectionDetectionMethod.CollectionFile
                };

                if (File.Exists(collectionFilePath))
                {
                    var jsonContent = File.ReadAllText(collectionFilePath);
                    var fileData = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent);

                    if (fileData != null)
                    {
                        // Parse collection settings from JSON
                        if (fileData.TryGetValue("name", out var nameObj) && nameObj is JsonElement nameElement)
                            collectionInfo.Name = nameElement.GetString() ?? collectionInfo.Name;

                        if (fileData.TryGetValue("order", out var orderObj) && orderObj is JsonElement orderElement)
                        {
                            if (Enum.TryParse<CollectionOrder>(orderElement.GetString(), true, out var order))
                                collectionInfo.Order = order;
                        }

                        if (fileData.TryGetValue("autoAdvance", out var autoAdvanceObj) && autoAdvanceObj is JsonElement autoAdvanceElement)
                            collectionInfo.AutoAdvance = autoAdvanceElement.GetBoolean();

                        if (fileData.TryGetValue("delay", out var delayObj) && delayObj is JsonElement delayElement)
                            collectionInfo.AutoAdvanceDelay = delayElement.GetInt32();
                    }
                }

                return collectionInfo;
            }
            catch (Exception)
            {
                // If file parsing fails, fall back to basic collection
                return CreateCollectionFromFolder(folderPath, CollectionDetectionMethod.CollectionFile);
            }
        }

        /// <summary>
        /// Creates a basic collection from folder information
        /// </summary>
        /// <param name="folderPath">Path to the folder</param>
        /// <param name="detectionMethod">How this collection was detected</param>
        /// <returns>CollectionInfo object</returns>
        private CollectionInfo CreateCollectionFromFolder(string folderPath, CollectionDetectionMethod detectionMethod)
        {
            var folderName = Path.GetFileName(folderPath);
            
            // Clean up the name by removing collection indicators
            var cleanName = folderName;
            foreach (var prefix in _collectionNamingPrefixes)
            {
                if (cleanName.StartsWith(prefix))
                {
                    cleanName = cleanName.Substring(prefix.Length).Trim();
                    break;
                }
            }

            foreach (var suffix in _collectionNamingSuffixes)
            {
                if (cleanName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    cleanName = cleanName.Substring(0, cleanName.Length - suffix.Length).Trim();
                    break;
                }
            }

            return new CollectionInfo
            {
                FolderPath = folderPath,
                Name = cleanName,
                DetectionMethod = detectionMethod,
                Order = CollectionOrder.Alphabetical,
                AutoAdvance = false,
                AutoAdvanceDelay = 3000
            };
        }

        /// <summary>
        /// Checks if a folder is inside a _collections directory
        /// </summary>
        /// <param name="folderPath">Path to check</param>
        /// <returns>True if inside _collections folder</returns>
        private bool IsInCollectionsFolder(string folderPath)
        {
            var currentPath = folderPath;
            while (!string.IsNullOrEmpty(currentPath))
            {
                var parentDir = Path.GetDirectoryName(currentPath);
                if (string.IsNullOrEmpty(parentDir))
                    break;

                var parentName = Path.GetFileName(parentDir);
                if (string.Equals(parentName, "_collections", StringComparison.OrdinalIgnoreCase))
                    return true;

                currentPath = parentDir;
            }
            return false;
        }

        /// <summary>
        /// Checks if a folder name matches special naming conventions
        /// </summary>
        /// <param name="folderName">Name of the folder</param>
        /// <returns>True if it matches special naming</returns>
        private bool HasSpecialNaming(string folderName)
        {
            // Check for prefixes
            foreach (var prefix in _collectionNamingPrefixes)
            {
                if (folderName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // Check for suffixes
            foreach (var suffix in _collectionNamingSuffixes)
            {
                if (folderName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Populates the images list for a collection based on its ordering preference
        /// </summary>
        /// <param name="collection">Collection to populate</param>
        /// <param name="imageFiles">List of image files in the collection</param>
        public void PopulateCollectionImages(CollectionInfo collection, List<ImageFile> imageFiles)
        {
            if (collection == null || imageFiles == null || imageFiles.Count == 0)
                return;

            collection.Images.Clear();

            switch (collection.Order)
            {
                case CollectionOrder.Alphabetical:
                    collection.Images = imageFiles.OrderBy(img => img.FileName).ToList();
                    break;

                case CollectionOrder.DateCreated:
                    collection.Images = imageFiles.OrderBy(img => File.GetCreationTime(img.FilePath)).ToList();
                    break;

                case CollectionOrder.DateModified:
                    collection.Images = imageFiles.OrderBy(img => File.GetLastWriteTime(img.FilePath)).ToList();
                    break;

                case CollectionOrder.FileSize:
                    collection.Images = imageFiles.OrderBy(img => new FileInfo(img.FilePath).Length).ToList();
                    break;

                case CollectionOrder.Random:
                    var random = new Random();
                    collection.Images = imageFiles.OrderBy(x => random.Next()).ToList();
                    break;

                default:
                    collection.Images = imageFiles.ToList();
                    break;
            }

            collection.CurrentIndex = 0;
            collection.IsComplete = false;
        }

        /// <summary>
        /// Gets the next image in a collection
        /// </summary>
        /// <param name="collection">Collection to get next image from</param>
        /// <returns>Next ImageFile or null if collection is complete</returns>
        public ImageFile GetNextImageInCollection(CollectionInfo collection)
        {
            if (collection == null || collection.Images.Count == 0)
                return null;

            if (collection.CurrentIndex >= collection.Images.Count)
            {
                collection.IsComplete = true;
                return null;
            }

            var image = collection.Images[collection.CurrentIndex];
            collection.CurrentIndex++;
            return image;
        }

        /// <summary>
        /// Gets the previous image in a collection
        /// </summary>
        /// <param name="collection">Collection to get previous image from</param>
        /// <returns>Previous ImageFile or null if at beginning</returns>
        public ImageFile GetPreviousImageInCollection(CollectionInfo collection)
        {
            if (collection == null || collection.Images.Count == 0)
                return null;

            if (collection.CurrentIndex <= 1)
                return null;

            collection.CurrentIndex--;
            return collection.Images[collection.CurrentIndex - 1];
        }

        /// <summary>
        /// Checks if a collection has more images
        /// </summary>
        /// <param name="collection">Collection to check</param>
        /// <returns>True if there are more images</returns>
        public bool HasMoreImages(CollectionInfo collection)
        {
            return collection != null && collection.CurrentIndex < collection.Images.Count;
        }

        /// <summary>
        /// Resets a collection to the beginning
        /// </summary>
        /// <param name="collection">Collection to reset</param>
        public void ResetCollection(CollectionInfo collection)
        {
            if (collection != null)
            {
                collection.CurrentIndex = 0;
                collection.IsComplete = false;
            }
        }
    }
}
