using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using RandomImageViewer.Models;
using RandomImageViewer.Services;
using XamlAnimatedGif;

namespace RandomImageViewer
{
    /// <summary>
    /// Main window for the Random Image Viewer application
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ImageManager _imageManager;
        private readonly DisplayEngine _displayEngine;
        private ImageFile _currentImage;
        private string _selectedFolder;
        private bool _isFullscreen = false;
        private WindowState _previousWindowState;
        private WindowStyle _previousWindowStyle;
        private bool _previousTopmost;
        private DisplaySettings _displaySettings;
        private readonly Stack<ImageFile> _navigationHistory;
        private readonly Stack<ImageFile> _forwardHistory; // For forward navigation through history

        public MainWindow()
        {
            InitializeComponent();
            
            _imageManager = new ImageManager();
            _displayEngine = new DisplayEngine();
            _displaySettings = new DisplaySettings();
            _navigationHistory = new Stack<ImageFile>();
            _forwardHistory = new Stack<ImageFile>();
            
            // Subscribe to events
            _imageManager.ScanProgressChanged += OnScanProgressChanged;
            _imageManager.ScanCompleted += OnScanCompleted;
            _imageManager.ReadyToStart += OnReadyToStart;
            _imageManager.CollectionStarted += OnCollectionStarted;
            _imageManager.CollectionCompleted += OnCollectionCompleted;
            _displayEngine.ImageLoadError += OnImageLoadError;
            
            // Set focus to window for keyboard input
            Loaded += (s, e) => Focus();
            
            // Add global key handler for fullscreen
            AddHandler(KeyDownEvent, new KeyEventHandler(GlobalKeyDown), true);
            
            // Load display settings
            LoadDisplaySettings();
        }

        private async void SelectFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Folder Containing Images",
                InitialDirectory = _selectedFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            if (dialog.ShowDialog() == true)
            {
                _selectedFolder = dialog.FolderName;
                await LoadImagesFromFolder(_selectedFolder);
            }
        }

        private async Task LoadImagesFromFolder(string folderPath)
        {
            try
            {
                StatusText.Text = "Scanning folder for images...";
                NextImageButton.IsEnabled = false;
                SelectFolderButton.IsEnabled = false;
                
                await _imageManager.ScanFolderAsync(folderPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading images: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error loading images";
                NextImageButton.IsEnabled = false;
                SelectFolderButton.IsEnabled = true;
            }
        }

        private void OnScanProgressChanged(object sender, int processedCount)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressText.Text = $"Processing: {processedCount} files...";
            });
        }

        private void OnReadyToStart(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = $"Ready to start! Found {_imageManager.TotalImageCount} images so far...";
                NextImageButton.IsEnabled = _imageManager.HasImages;
                PreviousImageButton.IsEnabled = false; // No history yet
                
                if (_imageManager.HasImages)
                {
                    // Load the first image immediately
                    LoadNextImage();
                }
            });
        }

        private void OnCollectionStarted(object sender, CollectionInfo collection)
        {
            Dispatcher.Invoke(() =>
            {
                if (!_isFullscreen)
                {
                    StatusText.Text = $"Collection: {collection.Name} ({collection.Images.Count} images)";
                }
            });
        }

        private void OnCollectionCompleted(object sender, CollectionInfo collection)
        {
            Dispatcher.Invoke(() =>
            {
                if (!_isFullscreen)
                {
                    StatusText.Text = $"Collection '{collection.Name}' completed. Resuming random display.";
                }
            });
        }

        private void OnScanCompleted(object sender, string message)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = message;
                ProgressText.Text = "";
                NextImageButton.IsEnabled = _imageManager.HasImages;
                PreviousImageButton.IsEnabled = false; // No history yet
                SelectFolderButton.IsEnabled = true;
                
                // Only load first image if we haven't already started
                if (_imageManager.HasImages && _currentImage == null)
                {
                    LoadNextImage();
                }
            });
        }

        private void LoadNextImage()
        {
            try
            {
                // First, check if we have forward history to navigate through
                if (_forwardHistory.Count > 0)
                {
                    // Navigate forward through history
                    var forwardImage = _forwardHistory.Pop();
                    
                    // Add current image to backward history
                    if (_currentImage != null)
                    {
                        _navigationHistory.Push(_currentImage);
                    }
                    
                    _currentImage = forwardImage;
                    DisplayImage(forwardImage);
                    
                    // Update button states
                    NextImageButton.IsEnabled = _imageManager.HasImages || _forwardHistory.Count > 0;
                    PreviousImageButton.IsEnabled = _navigationHistory.Count > 0;
                    return;
                }

                // No forward history, proceed with normal random navigation
                // Add current image to history before moving to next
                if (_currentImage != null)
                {
                    _navigationHistory.Push(_currentImage);
                }

                // Clear forward history since we're moving to new random images
                _forwardHistory.Clear();

                var nextImage = _imageManager.GetNextImage();
                if (nextImage == null)
                {
                    StatusText.Text = "No more images available. Resetting...";
                    _imageManager.ResetRemainingImages();
                    nextImage = _imageManager.GetNextImage();
                }

                if (nextImage != null)
                {
                    _currentImage = nextImage;
                    DisplayImage(nextImage);
                    
                    // Update button states
                    NextImageButton.IsEnabled = _imageManager.HasImages;
                    PreviousImageButton.IsEnabled = _navigationHistory.Count > 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading next image: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisplayImage(ImageFile imageFile)
        {
            try
            {
                var bitmap = _displayEngine.LoadImage(imageFile);
                if (bitmap != null)
                {
                    // Check if this is a GIF file
                    if (imageFile.FilePath.ToLowerInvariant().EndsWith(".gif"))
                    {
                        // For GIF files, use XamlAnimatedGif
                        AnimationBehavior.SetSourceUri(MainImage, new Uri(imageFile.FilePath));
                        AnimationBehavior.SetAutoStart(MainImage, true);
                        AnimationBehavior.SetRepeatBehavior(MainImage, System.Windows.Media.Animation.RepeatBehavior.Forever);
                        
                        // Don't scale animated GIFs - display at original size
                        MainImage.Width = double.NaN; // Auto size
                        MainImage.Height = double.NaN; // Auto size
                    }
                    else
                    {
                        // For static images, use normal bitmap display
                        AnimationBehavior.SetSourceUri(MainImage, null); // Clear any GIF animation
                        MainImage.Source = bitmap;
                        
                        // Set the image dimensions to fit the available space
                        SetImageDimensions(bitmap);
                    }
                    
                    // Update status information (only if not in fullscreen)
                    if (!_isFullscreen)
                    {
                        ImageInfoText.Text = _displayEngine.GetImageInfo(imageFile, bitmap);
                        StatusText.Text = $"Showing: {imageFile.FileName} ({_imageManager.RemainingImageCount} remaining)";
                    }
                    
                    // Reset scroll position
                    ImageScrollViewer.ScrollToHorizontalOffset(0);
                    ImageScrollViewer.ScrollToVerticalOffset(0);
                }
                else
                {
                    if (!_isFullscreen)
                    {
                        StatusText.Text = "Failed to load image";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error displaying image: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void NextImageButton_Click(object sender, RoutedEventArgs e)
        {
            LoadNextImage();
        }

        private void PreviousImageButton_Click(object sender, RoutedEventArgs e)
        {
            LoadPreviousImage();
        }

        private void LoadPreviousImage()
        {
            try
            {
                if (_navigationHistory.Count > 0)
                {
                    // Get the previous image from history
                    var previousImage = _navigationHistory.Pop();
                    
                    // Add current image to forward history (so we can navigate back to it)
                    if (_currentImage != null)
                    {
                        _forwardHistory.Push(_currentImage);
                    }
                    
                    _currentImage = previousImage;
                    DisplayImage(previousImage);
                    
                    // Update button states
                    NextImageButton.IsEnabled = _imageManager.HasImages || _forwardHistory.Count > 0;
                    PreviousImageButton.IsEnabled = _navigationHistory.Count > 0;
                }
                else
                {
                    if (!_isFullscreen)
                    {
                        StatusText.Text = "No previous images in history";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading previous image: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void JumpToEndOfHistory()
        {
            try
            {
                // If we have forward history, jump to the end (most recent image)
                if (_forwardHistory.Count > 0)
                {
                    // Add current image to backward history
                    if (_currentImage != null)
                    {
                        _navigationHistory.Push(_currentImage);
                    }

                    // Move all forward history items to backward history except the last one
                    while (_forwardHistory.Count > 1)
                    {
                        _navigationHistory.Push(_forwardHistory.Pop());
                    }

                    // Get the last (most recent) image from forward history
                    var endImage = _forwardHistory.Pop();
                    _currentImage = endImage;
                    DisplayImage(endImage);

                    // Update button states
                    NextImageButton.IsEnabled = _imageManager.HasImages;
                    PreviousImageButton.IsEnabled = _navigationHistory.Count > 0;
                }
                // If no forward history, we're already at the end - do nothing
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error jumping to end of history: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteCurrentImage()
        {
            if (_currentImage == null) return;

            try
            {
                // Show confirmation dialog
                var result = MessageBox.Show(
                    $"Are you sure you want to delete '{_currentImage.FileName}'?\n\nThis action cannot be undone.",
                    "Delete Image",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    // Delete the file from disk
                    if (File.Exists(_currentImage.FilePath))
                    {
                        File.Delete(_currentImage.FilePath);
                    }

                    // Remove from image manager
                    _imageManager.RemoveImage(_currentImage);

                    // Remove from navigation history if it exists there
                    var historyArray = _navigationHistory.ToArray();
                    _navigationHistory.Clear();
                    foreach (var img in historyArray)
                    {
                        if (!img.Equals(_currentImage))
                        {
                            _navigationHistory.Push(img);
                        }
                    }

                    // Remove from forward history if it exists there
                    var forwardHistoryArray = _forwardHistory.ToArray();
                    _forwardHistory.Clear();
                    foreach (var img in forwardHistoryArray)
                    {
                        if (!img.Equals(_currentImage))
                        {
                            _forwardHistory.Push(img);
                        }
                    }

                    // Load next image
                    var nextImage = _imageManager.GetNextRandomImage();
                    if (nextImage != null)
                    {
                        _currentImage = nextImage;
                        DisplayImage(nextImage);
                    }
                    else
                    {
                        // No more images
                        _currentImage = null;
                        MainImage.Source = null;
                        if (!_isFullscreen)
                        {
                            StatusText.Text = "No more images available";
                            ImageInfoText.Text = "No image loaded";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting image: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetImageDimensions(BitmapSource bitmap)
        {
            if (bitmap == null) return;

            // Get the available space
            var availableSize = GetAvailableImageSize();
            
            // Check if the image is larger than the available space
            bool isImageTooWide = bitmap.PixelWidth > availableSize.Width;
            bool isImageTooTall = bitmap.PixelHeight > availableSize.Height;
            
            if (!isImageTooWide && !isImageTooTall)
            {
                // Image fits, use original dimensions
                MainImage.Width = bitmap.PixelWidth;
                MainImage.Height = bitmap.PixelHeight;
                System.Diagnostics.Debug.WriteLine($"Image fits, using original size: {bitmap.PixelWidth}x{bitmap.PixelHeight}");
            }
            else
            {
                // Calculate the scale factor to fit the image within the available space
                double scaleX = availableSize.Width / bitmap.PixelWidth;
                double scaleY = availableSize.Height / bitmap.PixelHeight;
                double scale = Math.Min(scaleX, scaleY);
                
                // Set the image dimensions
                MainImage.Width = bitmap.PixelWidth * scale;
                MainImage.Height = bitmap.PixelHeight * scale;
                
                System.Diagnostics.Debug.WriteLine($"Scaling image to: {MainImage.Width}x{MainImage.Height} (scale: {scale})");
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Re-display the current image with new scaling when window size changes
            if (_currentImage != null)
            {
                DisplayImage(_currentImage);
            }
        }


        private Size GetAvailableImageSize()
        {
            double availableWidth, availableHeight;

            if (_isFullscreen)
            {
                // In fullscreen, use the entire screen size
                availableWidth = SystemParameters.PrimaryScreenWidth;
                availableHeight = SystemParameters.PrimaryScreenHeight;
            }
            else
            {
                // In windowed mode, use the actual image canvas size
                // Get the ImageBorder's actual size, which represents the image canvas
                var imageBorder = ImageBorder;
                availableWidth = imageBorder.ActualWidth;
                availableHeight = imageBorder.ActualHeight;
                
                // If the border hasn't been rendered yet, calculate from window size
                if (availableWidth <= 0 || availableHeight <= 0)
                {
                    var windowWidth = ActualWidth;
                    var windowHeight = ActualHeight;
                    
                    // Subtract space for toolbar (top) and status bar (bottom)
                    // Toolbar is typically around 40px, status bar around 30px
                    var uiHeight = 70;
                    
                    // Account for margins and borders (5px on each side = 10px total)
                    var margins = 10;
                    
                    availableWidth = windowWidth - margins;
                    availableHeight = windowHeight - uiHeight - margins;
                }
                
                // Add a small buffer to account for borders, padding, and rendering differences
                // The ImageBorder has BorderThickness="1" and there might be internal padding
                var buffer = 4; // 2 pixels on each side
                availableWidth = Math.Max(0, availableWidth - buffer);
                availableHeight = Math.Max(0, availableHeight - buffer);
                
                // Debug output
                System.Diagnostics.Debug.WriteLine($"ImageBorder: {imageBorder.ActualWidth}x{imageBorder.ActualHeight}, Available: {availableWidth}x{availableHeight}");
            }

            // Fallback to reasonable defaults if calculations fail
            if (availableWidth <= 0) availableWidth = 800;
            if (availableHeight <= 0) availableHeight = 600;

            return new Size(availableWidth, availableHeight);
        }


        private void GlobalKeyDown(object sender, KeyEventArgs e)
        {
            // Handle global shortcuts that should work regardless of focus
            switch (e.Key)
            {
                case Key.Space:
                    // Global next image - always works
                    if (_imageManager.HasImages)
                    {
                        LoadNextImage();
                    }
                    e.Handled = true;
                    break;
                    
                case Key.Enter:
                    // Global fullscreen toggle - always works
                    ToggleFullscreen();
                    e.Handled = true;
                    break;
                    
                case Key.F11:
                    // Global fullscreen toggle - always works
                    ToggleFullscreen();
                    e.Handled = true;
                    break;
                    
                case Key.Escape:
                    if (_isFullscreen)
                    {
                        ExitFullscreen();
                    }
                    else
                    {
                        // Exit application when Escape is pressed in normal window mode
                        Close();
                    }
                    e.Handled = true;
                    break;
                    
                case Key.Q:
                    // Exit application when Q is pressed
                    Close();
                    e.Handled = true;
                    break;
                    
                case Key.End:
                    // Jump to the end of forward history (most recent image)
                    JumpToEndOfHistory();
                    e.Handled = true;
                    break;
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Handle navigation shortcuts (only when window has focus)
            switch (e.Key)
            {
                case Key.Back:
                    // Backspace key for previous image
                    LoadPreviousImage();
                    e.Handled = true;
                    break;
                    
                case Key.Delete:
                    // Delete key for image deletion with confirmation
                    DeleteCurrentImage();
                    e.Handled = true;
                    break;
            }
        }

        private void ToggleFullscreen()
        {
            if (_isFullscreen)
            {
                ExitFullscreen();
            }
            else
            {
                EnterFullscreen();
            }
        }

        private void EnterFullscreen()
        {
            if (_isFullscreen) return;

            // Store current window properties
            _previousWindowState = WindowState;
            _previousWindowStyle = WindowStyle;
            _previousTopmost = Topmost;

            // Hide all UI elements
            TopToolbar.Visibility = Visibility.Collapsed;
            BottomStatusBar.Visibility = Visibility.Collapsed;
            ImageBorder.BorderThickness = new Thickness(0);
            ImageBorder.Margin = new Thickness(0);

            // Set window to true fullscreen
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;

            _isFullscreen = true;

            // Force a layout update and rescale the image for fullscreen
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateLayout();
                if (_currentImage != null)
                {
                    DisplayImage(_currentImage);
                }
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private void ExitFullscreen()
        {
            if (!_isFullscreen) return;

            // Restore window properties
            WindowStyle = _previousWindowStyle;
            WindowState = _previousWindowState;
            Topmost = _previousTopmost;
            ResizeMode = ResizeMode.CanResize;

            // Show UI elements
            TopToolbar.Visibility = Visibility.Visible;
            BottomStatusBar.Visibility = Visibility.Visible;
            ImageBorder.BorderThickness = new Thickness(1);
            ImageBorder.Margin = new Thickness(5);

            _isFullscreen = false;

            // Force a layout update and rescale the image for the new window size
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateLayout();
                if (_currentImage != null)
                {
                    DisplayImage(_currentImage);
                }
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private void OnImageLoadError(object sender, Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = $"Error loading image: {ex.Message}";
            });
        }

        private void LoadDisplaySettings()
        {
            try
            {
                // Set window size and position from settings
                if (_displaySettings.WindowSize.Width > 0 && _displaySettings.WindowSize.Height > 0)
                {
                    Width = _displaySettings.WindowSize.Width;
                    Height = _displaySettings.WindowSize.Height;
                }
                
                if (_displaySettings.WindowLocation.X > 0 && _displaySettings.WindowLocation.Y > 0)
                {
                    Left = _displaySettings.WindowLocation.X;
                    Top = _displaySettings.WindowLocation.Y;
                }
                
                WindowState = _displaySettings.WindowState;
            }
            catch (Exception ex)
            {
                // If loading settings fails, use defaults
                System.Diagnostics.Debug.WriteLine($"Error loading display settings: {ex.Message}");
            }
        }

        private void SaveDisplaySettings()
        {
            try
            {
                _displaySettings.WindowSize = new Size(Width, Height);
                _displaySettings.WindowLocation = new Point(Left, Top);
                _displaySettings.WindowState = WindowState;
                _displaySettings.IsFullscreen = _isFullscreen;
                _displaySettings.LastSelectedFolder = _selectedFolder;
                
                // In a real application, you would save these to a file or registry
                // For now, we'll just keep them in memory
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving display settings: {ex.Message}");
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            SaveDisplaySettings();
            _displayEngine.DisposeCurrentImage();
            base.OnClosed(e);
        }
    }
}
