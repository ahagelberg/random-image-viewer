# Random Image Viewer

A Windows GUI application for viewing images in random order from selected folders.

## Features (Phase 1)

- **Folder Selection**: Select a folder and recursively scan for JPEG, PNG, and GIF images (including animated GIFs)
- **Random Display**: Show images in random order without repetition
- **Keyboard Navigation**: 
  - `Space` - Next random image (or step through forward history)
  - `Backspace` - Previous image (navigation history)
  - `End` - Jump to end of history (most recent image)
  - `Delete` - Delete current image (with confirmation)
  - `Enter` - Toggle fullscreen mode (shows only the image, hides all UI)
  - `F11` - Alternative fullscreen toggle
  - `Escape` - Exit fullscreen mode (or exit application if not in fullscreen)
  - `Q` - Quit application
- **Image Scaling**: Static images automatically scale to fit the display area while maintaining aspect ratios (animated GIFs display at original size)
- **Progress Tracking**: Shows scan progress and remaining image count
- **Error Handling**: Graceful handling of corrupted or inaccessible files
- **Display Preferences**: Remembers window size, position, and display settings
- **Memory Optimization**: Efficient image loading and disposal to prevent memory leaks

## Phase 2 Features (Display Modes)

- **Improved Image Scaling**: Images now properly scale to fit the display area while maintaining aspect ratios
- **Display Settings Persistence**: Application remembers window size, position, and display preferences
- **Memory Management**: Optimized image loading and disposal to prevent memory leaks during extended use
- **Enhanced Fullscreen**: True fullscreen mode that shows only the image with no UI distractions

## Phase 3 Features (Navigation System)

- **Navigation History**: Track viewed images to allow backward navigation
- **Backward Navigation**: Use Backspace key to go back to previously viewed images
- **Image Deletion**: Delete images with confirmation dialog using Delete key
- **Smart History Management**: Automatically manages navigation history and remaining images
- **Enhanced UI**: Previous/Next buttons with proper state management

## Phase 4 Features (Performance & Polish)

- **Progressive Loading**: Start showing images after finding just 10 images (instead of waiting for complete scan)
- **Network Share Optimization**: Optimized scanning for network shares with 6000+ files
- **Batch Processing**: Process files in batches of 50 for better performance
- **Parallel File Discovery**: Use parallel processing for initial file discovery
- **Image Loading Optimization**: Optimized caching and memory management for faster loading
- **UI Responsiveness**: Small delays prevent UI freezing on large folders

## Requirements

- Windows 10/11
- .NET 6.0 or later

## Building and Running

1. Open `RandomImageViewer.sln` in Visual Studio 2022 or later
2. Build the solution (Ctrl+Shift+B)
3. Run the application (F5)

## Usage

1. Launch the application
2. Click "Select Folder" to choose a folder containing images
3. Wait for the folder scan to complete
4. Use the Space key to navigate through images in random order
5. Use Backspace to go back to previously viewed images
6. Use End to jump to the most recent image (if you've stepped back in history)
7. Use Delete to remove unwanted images (with confirmation)
8. Use Enter to toggle fullscreen mode for better viewing (F11 also works)
9. Press Esc or Q to exit the application

## Supported Image Formats

- JPEG (.jpg, .jpeg)
- PNG (.png)
- GIF (.gif) - Both static and animated GIFs

## Future Features (Planned)

- Backward navigation (Backspace key)
- Image deletion with confirmation (Delete key)
- Zoom controls (+/- keys)
- Pan controls (arrow keys)
- Special folder support
- Additional image format support (GIF, WebP, TIFF, etc.)

## Technical Details

- Built with C# and WPF
- Asynchronous folder scanning for responsive UI
- Memory-efficient image loading and caching
- Thread-safe image operations
- Hardware-accelerated rendering

## Project Structure

```
RandomImageViewer/
├── Models/           # Data models (ImageFile, DisplaySettings)
├── Services/         # Core services (ImageManager, DisplayEngine)
├── MainWindow.xaml   # Main UI
├── App.xaml         # Application entry point
└── README.md        # This file
```
