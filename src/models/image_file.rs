use std::ffi::OsStr;
use std::path::{Path, PathBuf};
use std::time::SystemTime;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ImageFormat {
    Unknown,
    Jpeg,
    Png,
    Gif,
    WebP,
}

impl ImageFormat {
    /// Format of `path`, guessed from its extension.
    pub fn from_path(path: &Path) -> Self {
        match path
            .extension()
            .and_then(OsStr::to_str)
            .map(str::to_ascii_lowercase)
            .as_deref()
        {
            Some("jpg" | "jpeg") => Self::Jpeg,
            Some("png") => Self::Png,
            Some("gif") => Self::Gif,
            Some("webp") => Self::WebP,
            _ => Self::Unknown,
        }
    }
}

/// An image on disk plus the metadata the viewer needs.
#[derive(Debug, Clone)]
pub struct ImageFile {
    pub path: PathBuf,
    pub name: String,
    pub size: u64,
    pub modified: SystemTime,
    pub format: ImageFormat,
}

impl ImageFile {
    pub fn new(path: impl Into<PathBuf>) -> Self {
        let path = path.into();
        let name = path
            .file_name()
            .map(|name| name.to_string_lossy().into_owned())
            .unwrap_or_default();
        let (size, modified) = match std::fs::metadata(&path) {
            Ok(meta) => (
                meta.len(),
                meta.modified().unwrap_or(SystemTime::UNIX_EPOCH),
            ),
            Err(_) => (0, SystemTime::UNIX_EPOCH),
        };
        let format = ImageFormat::from_path(&path);
        Self {
            path,
            name,
            size,
            modified,
            format,
        }
    }
}

impl PartialEq for ImageFile {
    fn eq(&self, other: &Self) -> bool {
        self.path
            .to_string_lossy()
            .eq_ignore_ascii_case(&other.path.to_string_lossy())
    }
}

impl Eq for ImageFile {}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn maps_extensions() {
        assert_eq!(
            ImageFormat::from_path(Path::new("a.JPG")),
            ImageFormat::Jpeg
        );
        assert_eq!(
            ImageFormat::from_path(Path::new("a.jpeg")),
            ImageFormat::Jpeg
        );
        assert_eq!(
            ImageFormat::from_path(Path::new("a.WebP")),
            ImageFormat::WebP
        );
        assert_eq!(
            ImageFormat::from_path(Path::new("a.txt")),
            ImageFormat::Unknown
        );
    }

    #[test]
    fn equality_ignores_path_case() {
        let a = ImageFile::new("/tmp/Pic.PNG");
        let b = ImageFile::new("/tmp/pic.png");
        assert_eq!(a, b);
    }
}
