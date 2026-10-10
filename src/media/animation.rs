use std::fs::File;
use std::io::BufReader;
use std::path::Path;
use std::time::Duration;

use eframe::egui::ColorImage;
use image::AnimationDecoder;

use crate::media::decode;

/// Duration used when a frame reports no delay.
const DEFAULT_FRAME_MS: u32 = 100;

/// A decoded frame sequence with timing, played back by the UI loop.
pub struct Animation {
    frames: Vec<ColorImage>,
    durations_ms: Vec<u32>,
    index: usize,
    elapsed: Duration,
}

impl Animation {
    pub fn load(path: &Path) -> Result<Self, String> {
        if decode::is_gif(path) {
            Self::load_gif(path)
        } else {
            Self::load_webp(path)
        }
    }

    pub fn frame(&self) -> &ColorImage {
        &self.frames[self.index]
    }

    pub fn dimensions(&self) -> [u32; 2] {
        let size = self.frames[self.index].size;
        [size[0] as u32, size[1] as u32]
    }

    /// Advances playback by `delta`; returns whether the visible frame changed.
    pub fn advance(&mut self, delta: Duration) -> bool {
        if self.frames.len() < 2 {
            return false;
        }
        self.elapsed += delta;
        let mut changed = false;
        loop {
            let duration = self.frame_duration();
            if self.elapsed < duration {
                break;
            }
            self.elapsed -= duration;
            self.index = (self.index + 1) % self.frames.len();
            changed = true;
        }
        changed
    }

    /// Time left before the visible frame changes.
    pub fn time_to_next_frame(&self) -> Duration {
        self.frame_duration().saturating_sub(self.elapsed)
    }

    fn frame_duration(&self) -> Duration {
        Duration::from_millis(u64::from(self.durations_ms[self.index]))
    }

    fn load_gif(path: &Path) -> Result<Self, String> {
        let file = File::open(path).map_err(|error| error.to_string())?;
        let decoder = image::codecs::gif::GifDecoder::new(BufReader::new(file))
            .map_err(|error| error.to_string())?;
        let frames = decoder
            .into_frames()
            .collect_frames()
            .map_err(|error| error.to_string())?;
        if frames.is_empty() {
            return Err("no frames".to_owned());
        }
        let mut images = Vec::with_capacity(frames.len());
        let mut durations = Vec::with_capacity(frames.len());
        for frame in frames {
            let (numerator, denominator) = frame.delay().numer_denom_ms();
            durations.push(frame_duration_ms(numerator, denominator));
            images.push(rgba_to_color_image(frame.into_buffer()));
        }
        Ok(Self {
            frames: images,
            durations_ms: durations,
            index: 0,
            elapsed: Duration::ZERO,
        })
    }

    fn load_webp(path: &Path) -> Result<Self, String> {
        let file = File::open(path).map_err(|error| error.to_string())?;
        let mut decoder = image_webp::WebPDecoder::new(BufReader::new(file))
            .map_err(|error| error.to_string())?;
        if !decoder.is_animated() {
            return Err("not an animated webp".to_owned());
        }
        let (width, height) = decoder.dimensions();
        let has_alpha = decoder.has_alpha();
        let buffer_size = decoder.output_buffer_size().ok_or("webp frame too large")?;
        let mut buffer = vec![0u8; buffer_size];
        let mut images = Vec::new();
        let mut durations = Vec::new();
        loop {
            match decoder.read_frame(&mut buffer) {
                Ok(duration) => {
                    let size = [width as usize, height as usize];
                    let frame = if has_alpha {
                        ColorImage::from_rgba_unmultiplied(size, &buffer)
                    } else {
                        ColorImage::from_rgb(size, &buffer)
                    };
                    images.push(frame);
                    durations.push(duration.max(1));
                }
                Err(image_webp::DecodingError::NoMoreFrames) => break,
                Err(error) => return Err(error.to_string()),
            }
        }
        if images.is_empty() {
            return Err("no frames".to_owned());
        }
        Ok(Self {
            frames: images,
            durations_ms: durations,
            index: 0,
            elapsed: Duration::ZERO,
        })
    }
}

/// True when the WebP file holds more than one frame.
pub fn is_animated_webp(path: &Path) -> bool {
    let Ok(file) = File::open(path) else {
        return false;
    };
    let Ok(decoder) = image_webp::WebPDecoder::new(BufReader::new(file)) else {
        return false;
    };
    decoder.is_animated()
}

fn frame_duration_ms(numerator: u32, denominator: u32) -> u32 {
    if denominator == 0 {
        return DEFAULT_FRAME_MS;
    }
    (numerator / denominator).max(1)
}

fn rgba_to_color_image(buffer: image::RgbaImage) -> ColorImage {
    let size = [buffer.width() as usize, buffer.height() as usize];
    ColorImage::from_rgba_unmultiplied(size, buffer.as_raw())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn zero_denominator_uses_default() {
        assert_eq!(frame_duration_ms(50, 0), DEFAULT_FRAME_MS);
    }

    #[test]
    fn converts_numerator_over_denominator() {
        assert_eq!(frame_duration_ms(200, 100), 2);
    }

    #[test]
    fn zero_duration_becomes_one_ms() {
        assert_eq!(frame_duration_ms(0, 100), 1);
    }
}
