using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SkiaSharp;

namespace RandomImageViewer.Services
{
    /// <summary>
    /// Handles animated WebP file decoding and playback
    /// </summary>
    public class AnimatedWebPEngine
    {
        private readonly DispatcherTimer _animationTimer;
        private List<WriteableBitmap> _frames;
        private int _currentFrameIndex;
        private bool _isPlaying;
        private bool _isLooping;
        private int _frameCount;
        private int[] _frameDurations; // in milliseconds

        public event EventHandler<WriteableBitmap> FrameChanged;

        public AnimatedWebPEngine()
        {
            _animationTimer = new DispatcherTimer();
            _animationTimer.Tick += OnTimerTick;
            _frames = new List<WriteableBitmap>();
            _currentFrameIndex = 0;
            _isPlaying = false;
            _isLooping = true;
        }

        /// <summary>
        /// Loads an animated WebP file and extracts frames
        /// </summary>
        /// <param name="filePath">Path to the WebP file</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool LoadAnimatedWebP(string filePath)
        {
            try
            {
                _frames.Clear();
                _currentFrameIndex = 0;

                using (var stream = File.OpenRead(filePath))
                using (var codec = SKCodec.Create(stream))
                {
                    if (codec == null)
                        return false;

                    _frameCount = codec.FrameCount;
                    _frameDurations = new int[_frameCount];

                    // Get frame info
                    for (int i = 0; i < _frameCount; i++)
                    {
                        var frameInfo = codec.FrameInfo[i];
                        _frameDurations[i] = frameInfo.Duration;
                    }

                    // Decode all frames
                    for (int i = 0; i < _frameCount; i++)
                    {
                        var bitmap = new SKBitmap(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                        var options = new SKCodecOptions(i);
                        var result = codec.GetPixels(bitmap.Info, bitmap.GetPixels(), options);
                        
                        if (result == SKCodecResult.Success)
                        {
                            var wpfBitmap = ConvertSkiaToWpfBitmap(bitmap);
                            _frames.Add(wpfBitmap);
                        }
                        
                        bitmap.Dispose();
                    }
                }

                return _frames.Count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading animated WebP: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Starts animation playback
        /// </summary>
        public void StartAnimation()
        {
            if (_frames.Count > 1 && !_isPlaying)
            {
                _isPlaying = true;
                _currentFrameIndex = 0;
                PlayNextFrame();
            }
        }

        /// <summary>
        /// Stops animation playback
        /// </summary>
        public void StopAnimation()
        {
            _isPlaying = false;
            _animationTimer.Stop();
        }

        /// <summary>
        /// Gets the current frame
        /// </summary>
        /// <returns>Current frame as WriteableBitmap</returns>
        public WriteableBitmap GetCurrentFrame()
        {
            if (_frames.Count > 0 && _currentFrameIndex < _frames.Count)
            {
                return _frames[_currentFrameIndex];
            }
            return null;
        }

        /// <summary>
        /// Gets the first frame (for static display)
        /// </summary>
        /// <returns>First frame as WriteableBitmap</returns>
        public WriteableBitmap GetFirstFrame()
        {
            if (_frames.Count > 0)
            {
                return _frames[0];
            }
            return null;
        }

        /// <summary>
        /// Checks if the WebP file is animated
        /// </summary>
        /// <param name="filePath">Path to the WebP file</param>
        /// <returns>True if animated, false otherwise</returns>
        public static bool IsAnimatedWebP(string filePath)
        {
            try
            {
                using (var stream = File.OpenRead(filePath))
                using (var codec = SKCodec.Create(stream))
                {
                    return codec != null && codec.FrameCount > 1;
                }
            }
            catch
            {
                return false;
            }
        }

        private void PlayNextFrame()
        {
            if (!_isPlaying || _frames.Count == 0)
                return;

            // Notify that frame has changed
            FrameChanged?.Invoke(this, _frames[_currentFrameIndex]);

            // Set up timer for next frame
            if (_currentFrameIndex < _frameDurations.Length)
            {
                int duration = _frameDurations[_currentFrameIndex];
                if (duration <= 0)
                    duration = 100; // Default 100ms if duration is 0

                _animationTimer.Interval = TimeSpan.FromMilliseconds(duration);
                _animationTimer.Start();
            }
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            _animationTimer.Stop();

            if (!_isPlaying)
                return;

            _currentFrameIndex++;

            if (_currentFrameIndex >= _frames.Count)
            {
                if (_isLooping)
                {
                    _currentFrameIndex = 0;
                }
                else
                {
                    _isPlaying = false;
                    return;
                }
            }

            PlayNextFrame();
        }

        private WriteableBitmap ConvertSkiaToWpfBitmap(SKBitmap skiaBitmap)
        {
            var wpfBitmap = new WriteableBitmap(skiaBitmap.Width, skiaBitmap.Height, 96, 96, PixelFormats.Bgra32, null);
            
            wpfBitmap.Lock();
            var backBuffer = wpfBitmap.BackBuffer;
            var stride = wpfBitmap.BackBufferStride;
            
            unsafe
            {
                var srcPtr = (byte*)skiaBitmap.GetPixels().ToPointer();
                var dstPtr = (byte*)backBuffer.ToPointer();
                
                for (int y = 0; y < skiaBitmap.Height; y++)
                {
                    for (int x = 0; x < skiaBitmap.Width; x++)
                    {
                        var srcIndex = (y * skiaBitmap.Width + x) * 4; // BGRA
                        var dstIndex = y * stride + x * 4; // BGRA
                        
                        dstPtr[dstIndex] = srcPtr[srcIndex];     // B
                        dstPtr[dstIndex + 1] = srcPtr[srcIndex + 1]; // G
                        dstPtr[dstIndex + 2] = srcPtr[srcIndex + 2]; // R
                        dstPtr[dstIndex + 3] = srcPtr[srcIndex + 3]; // A
                    }
                }
            }
            
            wpfBitmap.AddDirtyRect(new Int32Rect(0, 0, skiaBitmap.Width, skiaBitmap.Height));
            wpfBitmap.Unlock();
            
            return wpfBitmap;
        }

        public void Dispose()
        {
            StopAnimation();
            _frames?.Clear();
        }
    }
}
