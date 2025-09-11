# Random Image Viewer - Project Plan

## Project Overview

A Windows GUI application for displaying images in random order from selected folders. The application focuses on keyboard-driven navigation with minimal external dependencies and high performance for handling thousands of images.

## Core Requirements

### Primary Features
- **Folder Selection**: Select a root folder and recursively scan for images
- **Image Format Support**: JPEG, PNG, GIF (static and animated), WebP (static and animated), with future support for other formats
- **Random Display**: Show images in random order without repetition
- **Navigation History**: Track viewed images to allow backward navigation
- **Keyboard Controls**: Space (next), Backspace (previous), Enter (fullscreen toggle), Delete (remove image), Esc and Q (quit)
- **Performance**: Handle thousands of images with near-instantaneous switching
- **Image Scaling**: Auto-fit large images to screen in both windowed and fullscreen modes

### Future Enhancements
- **Zoom Controls**: +/- keys for image zooming
- **Pan Controls**: Arrow keys for panning zoomed images
- **Special Folders**: Treat certain folders as "image containers" that display all contents sequentially
- **Extended Format Support**: GIF, animated GIF, WebP, TIFF, BMP, etc.

## Technical Architecture

### Technology Stack
**Primary Recommendation: C# with WPF**
- **Pros**: Native Windows performance, excellent image handling, minimal external dependencies
- **Image Processing**: Built-in BitmapSource and Image controls
- **File I/O**: System.IO for efficient folder scanning
- **Threading**: BackgroundTask for non-blocking image loading

**Alternative: C++ with Win32 API**
- **Pros**: Maximum performance, minimal dependencies
- **Cons**: More complex development, longer development time

### Core Components

#### 1. Image Manager
```csharp
public class ImageManager
{
    - ScanFolder(string path) -> List<ImageFile>
    - GetRandomImage() -> ImageFile
    - GetPreviousImage() -> ImageFile
    - DeleteImage(ImageFile image)
    - GetImageHistory() -> List<ImageFile>
}
```

#### 2. Display Engine
```csharp
public class DisplayEngine
{
    - LoadImage(ImageFile file) -> BitmapSource
    - ScaleImage(BitmapSource image, Size targetSize) -> BitmapSource
    - ToggleFullscreen()
    - SetDisplayMode(DisplayMode mode)
}
```

#### 3. Navigation Controller
```csharp
public class NavigationController
{
    - NextImage()
    - PreviousImage()
    - JumpToImage(int index)
    - GetCurrentPosition() -> int
    - GetHistoryStack() -> Stack<ImageFile>
}
```

#### 4. Input Handler
```csharp
public class InputHandler
{
    - HandleKeyPress(Key key)
    - RegisterShortcuts()
    - ProcessKeyboardInput()
}
```

## Performance Optimization Strategy

### Image Loading
- **Lazy Loading**: Load images only when needed
- **Caching**: Keep 2-3 images in memory (current, next, previous)
- **Background Loading**: Pre-load next image while displaying current
- **Memory Management**: Dispose unused images to prevent memory leaks

### File System Operations
- **Async Scanning**: Use async/await for folder scanning
- **Progress Reporting**: Show scan progress for large folders
- **Indexing**: Create in-memory index of all images for fast random access

### Rendering Optimization
- **Hardware Acceleration**: Use WPF's hardware acceleration
- **Image Decoding**: Use appropriate decoders for each format
- **Scaling Algorithms**: Implement efficient scaling for different image sizes

## Development Phases

### Phase 1: Core Foundation
**Deliverables:**
- Basic WPF application structure
- Folder selection dialog
- Image file discovery and indexing
- Basic image display functionality
- Simple keyboard input handling

**Key Features:**
- Select folder and scan for JPEG/PNG/GIF/WebP files
- Display first image
- Space key to show next random image
- Basic error handling

### Phase 2: Display Modes
**Deliverables:**
- Fullscreen mode toggle (Enter key)
- Image scaling and fitting
- Window state management
- Display mode persistence

**Key Features:**
- Toggle between windowed and fullscreen
- Auto-scale images to fit screen
- Maintain aspect ratios
- Remember display preferences

### Phase 3: Navigation System
**Deliverables:**
- Random image selection algorithm
- Navigation history tracking
- Backspace key for previous image
- Image deletion with confirmation
- Basic UI improvements

**Key Features:**
- Implement proper random selection without repetition
- History stack for backward navigation
- Delete confirmation dialog
- Improved keyboard shortcuts

### Phase 4: Performance & Polish
**Deliverables:**
- Performance optimization
- Memory management
- Error handling improvements
- User experience enhancements

**Key Features:**
- Optimize for thousands of images
- Implement proper caching
- Add loading indicators
- Improve error messages

### Phase 5: Future Enhancements (Weeks 9+)
**Deliverables:**
- Zoom functionality (+/- keys)
- Pan controls (arrow keys)
- Special folder support
- Additional image format support

## File Structure

```
RandomImageViewer/
├── src/
│   ├── Models/
│   │   ├── ImageFile.cs
│   │   ├── DisplaySettings.cs
│   │   └── NavigationState.cs
│   ├── Services/
│   │   ├── ImageManager.cs
│   │   ├── DisplayEngine.cs
│   │   ├── NavigationController.cs
│   │   └── InputHandler.cs
│   ├── Views/
│   │   ├── MainWindow.xaml
│   │   ├── MainWindow.xaml.cs
│   │   └── ImageDisplayControl.xaml
│   ├── ViewModels/
│   │   ├── MainViewModel.cs
│   │   └── ImageDisplayViewModel.cs
│   └── Utils/
│       ├── ImageUtils.cs
│       ├── FileUtils.cs
│       └── PerformanceUtils.cs
├── tests/
├── docs/
└── RandomImageViewer.sln
```

## Technical Specifications

### Supported Image Formats
**Phase 1:**
- JPEG (.jpg, .jpeg)
- PNG (.png)
- GIF (.gif) - static and animated
- WebP (.webp) - static and animated

**Future Phases:**
- TIFF (.tiff, .tif)
- BMP (.bmp)
- ICO (.ico)

### Performance Targets
- **Image Loading**: < 100ms for typical images
- **Folder Scanning**: < 5 seconds for 10,000 images
- **Memory Usage**: < 200MB for 5,000 image index
- **Startup Time**: < 2 seconds

### System Requirements
- **OS**: Windows 10/11
- **RAM**: 4GB minimum, 8GB recommended
- **Storage**: 100MB for application
- **Dependencies**: .NET 6.0 or later

## Risk Assessment

### Technical Risks
1. **Memory Management**: Large image collections could cause memory issues
   - *Mitigation*: Implement aggressive caching and disposal strategies

2. **Performance**: Slow image loading with large files
   - *Mitigation*: Background loading and progressive enhancement

3. **File System Access**: Permission issues with certain folders
   - *Mitigation*: Graceful error handling and user feedback

### Development Risks
1. **Scope Creep**: Feature requests beyond core functionality
   - *Mitigation*: Clear phase boundaries and feature prioritization

2. **Performance Requirements**: Meeting speed targets
   - *Mitigation*: Early performance testing and optimization

## Success Criteria

### Phase 1 Success
- Application launches and displays images
- Basic folder selection works
- Space key navigation functions

### Phase 2 Success
- Fullscreen mode works smoothly
- Images scale appropriately
- No memory leaks during extended use

### Phase 3 Success
- Random selection without repetition
- Backward navigation works
- Image deletion with confirmation

### Final Success
- Handles 5,000+ images smoothly
- All keyboard shortcuts work reliably
- Application feels responsive and professional

## Future Roadmap

### Version 2.0 Features
- Zoom and pan controls
- Special folder support
- Extended image format support
- Slideshow mode with timing controls

### Version 3.0 Features
- Image metadata display
- Basic image editing (rotate, flip)
- Favorites system
- Export functionality

### Long-term Vision
- Plugin system for custom image processors
- Cloud storage integration
- Mobile companion app
- Advanced image organization features

---

*This project plan provides a comprehensive roadmap for developing a professional-grade random image viewer application. The phased approach ensures steady progress while maintaining focus on core functionality and performance requirements.*
