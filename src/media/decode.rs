use std::fs::File;
use std::io::BufReader;
use std::path::Path;

use eframe::egui::ColorImage;
use image::DynamicImage;

use crate::models::image_file::ImageFormat;

pub fn is_supported(path: &Path) -> bool {
    ImageFormat::from_path(path) != ImageFormat::Unknown
}

pub fn is_gif(path: &Path) -> bool {
    ImageFormat::from_path(path) == ImageFormat::Gif
}

/// Decodes a still image of any supported format.
pub fn decode_static(path: &Path) -> Result<DynamicImage, String> {
    let file = File::open(path).map_err(|error| error.to_string())?;
    let reader = image::ImageReader::new(BufReader::new(file))
        .with_guessed_format()
        .map_err(|error| error.to_string())?;
    reader.decode().map_err(|error| error.to_string())
}

/// Converts a decoded image into an egui texture source and its pixel size.
pub fn to_color_image(image: &DynamicImage) -> (ColorImage, [u32; 2]) {
    let rgba = image.to_rgba8();
    let dimensions = [rgba.width(), rgba.height()];
    let size = [rgba.width() as usize, rgba.height() as usize];
    (
        ColorImage::from_rgba_unmultiplied(size, rgba.as_raw()),
        dimensions,
    )
}
