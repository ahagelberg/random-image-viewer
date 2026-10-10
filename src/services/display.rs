use std::ffi::OsStr;

use crate::models::image_file::ImageFile;

/// Size units used when formatting.
const SIZE_UNITS: [&str; 4] = ["B", "KB", "MB", "GB"];
/// Number of bytes in the next unit.
const SIZE_STEP: f64 = 1024.0;
/// Shown when a file has no recognizable image extension.
const UNKNOWN_FORMAT: &str = "Unknown";

pub fn image_info(file: &ImageFile, dimensions: [u32; 2]) -> String {
    format!(
        "{} ({}) - {}",
        file.name,
        dimensions_text(dimensions),
        format_file_size(file.size)
    )
}

fn dimensions_text(dimensions: [u32; 2]) -> String {
    format!("{} × {}", dimensions[0], dimensions[1])
}

/// Label/value pairs describing an image, for the info dialog. Timestamps are
/// RFC 3339 (ISO 8601) in UTC, never a local or US-style date.
pub fn image_details(file: &ImageFile, dimensions: [u32; 2]) -> Vec<(&'static str, String)> {
    let folder = file
        .path
        .parent()
        .map(|parent| parent.to_string_lossy().into_owned())
        .unwrap_or_default();
    let format = file
        .path
        .extension()
        .and_then(OsStr::to_str)
        .map(str::to_ascii_uppercase)
        .unwrap_or_else(|| UNKNOWN_FORMAT.to_owned());
    vec![
        ("Name", file.name.clone()),
        ("Folder", folder),
        ("Size", format_file_size(file.size)),
        ("Resolution", format!("{} px", dimensions_text(dimensions))),
        ("Format", format),
        (
            "Modified",
            humantime::format_rfc3339_seconds(file.modified).to_string(),
        ),
    ]
}

fn format_file_size(bytes: u64) -> String {
    let mut value = bytes as f64;
    let mut unit = 0;
    while value >= SIZE_STEP && unit < SIZE_UNITS.len() - 1 {
        unit += 1;
        value /= SIZE_STEP;
    }
    let number = if value.fract() == 0.0 {
        format!("{value:.0}")
    } else {
        format!("{value:.2}")
    };
    format!("{number} {}", SIZE_UNITS[unit])
}

#[cfg(test)]
mod tests {
    use std::path::PathBuf;

    use super::*;
    use crate::models::image_file::ImageFile;

    fn file_with_size(size: u64) -> ImageFile {
        let mut file = ImageFile::new(PathBuf::from("/nonexistent/image.png"));
        file.size = size;
        file
    }

    #[test]
    fn formats_sizes() {
        assert_eq!(format_file_size(512), "512 B");
        assert_eq!(format_file_size(2048), "2 KB");
        assert_eq!(format_file_size(1536), "1.50 KB");
    }

    #[test]
    fn includes_dimensions_and_name() {
        let text = image_info(&file_with_size(1024), [800, 600]);
        assert!(text.contains("image.png"));
        assert!(text.contains("800 × 600"));
        assert!(text.contains("1 KB"));
    }

    #[test]
    fn details_describe_the_file() {
        assert_eq!(
            image_details(&file_with_size(2048), [800, 600]),
            vec![
                ("Name", "image.png".to_owned()),
                ("Folder", "/nonexistent".to_owned()),
                ("Size", "2 KB".to_owned()),
                ("Resolution", "800 × 600 px".to_owned()),
                ("Format", "PNG".to_owned()),
                ("Modified", "1970-01-01T00:00:00Z".to_owned()),
            ]
        );
    }
}
