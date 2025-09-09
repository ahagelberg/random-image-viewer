using System;
using System.Collections.Generic;

namespace RandomImageViewer.Models
{
    /// <summary>
    /// Represents information about a special image collection folder
    /// </summary>
    public class CollectionInfo
    {
        /// <summary>
        /// The folder path of this collection
        /// </summary>
        public string FolderPath { get; set; }

        /// <summary>
        /// Display name for the collection
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// How the images should be ordered in the collection
        /// </summary>
        public CollectionOrder Order { get; set; } = CollectionOrder.Alphabetical;

        /// <summary>
        /// Whether to automatically advance through images
        /// </summary>
        public bool AutoAdvance { get; set; } = false;

        /// <summary>
        /// Delay between images in milliseconds (if auto-advance is enabled)
        /// </summary>
        public int AutoAdvanceDelay { get; set; } = 3000;

        /// <summary>
        /// How this collection was detected
        /// </summary>
        public CollectionDetectionMethod DetectionMethod { get; set; }

        /// <summary>
        /// List of images in this collection (in display order)
        /// </summary>
        public List<ImageFile> Images { get; set; } = new List<ImageFile>();

        /// <summary>
        /// Current index in the collection
        /// </summary>
        public int CurrentIndex { get; set; } = 0;

        /// <summary>
        /// Whether this collection has been fully displayed
        /// </summary>
        public bool IsComplete { get; set; } = false;
    }

    /// <summary>
    /// Defines how images in a collection should be ordered
    /// </summary>
    public enum CollectionOrder
    {
        /// <summary>
        /// Order by filename alphabetically
        /// </summary>
        Alphabetical,

        /// <summary>
        /// Order by file creation date
        /// </summary>
        DateCreated,

        /// <summary>
        /// Order by file modification date
        /// </summary>
        DateModified,

        /// <summary>
        /// Order by file size
        /// </summary>
        FileSize,

        /// <summary>
        /// Random order (but consistent within the collection)
        /// </summary>
        Random
    }

    /// <summary>
    /// Defines how a collection was detected
    /// </summary>
    public enum CollectionDetectionMethod
    {
        /// <summary>
        /// Detected by .collection file
        /// </summary>
        CollectionFile,

        /// <summary>
        /// Detected by being in _collections folder
        /// </summary>
        CollectionsFolder,

        /// <summary>
        /// Detected by special naming convention
        /// </summary>
        SpecialNaming
    }
}
