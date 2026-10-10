use std::path::Path;
use std::time::SystemTime;

use serde::Deserialize;

use crate::models::collection_info::{CollectionInfo, CollectionOrder};
use crate::models::image_file::ImageFile;
use crate::rng::Rng;

/// Marker file that promotes a folder to a collection.
const COLLECTION_FILE_NAME: &str = ".collection";
/// Any subfolder of a folder with this name is a collection.
const COLLECTIONS_DIR_NAME: &str = "_collections";
/// Folder-name prefixes that mark a collection.
const NAMING_PREFIXES: [&str; 3] = ["[COLLECTION]", "[ALBUM]", "[SEQUENCE]"];
/// Folder-name suffixes that mark a collection.
const NAMING_SUFFIXES: [&str; 3] = ["_collection", "_album", "_sequence"];

#[derive(Deserialize)]
struct CollectionFileConfig {
    name: Option<String>,
    order: Option<String>,
}

/// Detects collection folders and keeps their images in display order.
pub struct CollectionManager;

impl CollectionManager {
    pub fn new() -> Self {
        Self
    }

    pub fn detect(&self, folder: &Path) -> Option<CollectionInfo> {
        if !folder.is_dir() {
            return None;
        }
        let collection_file = folder.join(COLLECTION_FILE_NAME);
        if collection_file.is_file() {
            return Some(self.load_from_file(folder, &collection_file));
        }
        if is_in_collections_folder(folder) || has_special_naming(&folder_name(folder)) {
            return Some(self.create_from_folder(folder));
        }
        None
    }

    fn load_from_file(&self, folder: &Path, collection_file: &Path) -> CollectionInfo {
        let mut info = self.create_from_folder(folder);
        let Ok(text) = std::fs::read_to_string(collection_file) else {
            return info;
        };
        let Ok(config) = serde_json::from_str::<CollectionFileConfig>(&text) else {
            return info;
        };
        if let Some(name) = config.name
            && !name.trim().is_empty()
        {
            info.name = name;
        }
        if let Some(order) = config.order.as_deref().and_then(parse_order) {
            info.order = order;
        }
        info
    }

    fn create_from_folder(&self, folder: &Path) -> CollectionInfo {
        CollectionInfo::new(clean_name(&folder_name(folder)))
    }

    /// Orders the collection's images according to its configured order.
    pub fn populate(
        &self,
        collection: &mut CollectionInfo,
        mut images: Vec<ImageFile>,
        rng: &mut Rng,
    ) {
        if images.is_empty() {
            return;
        }
        match collection.order {
            CollectionOrder::Alphabetical => images.sort_by(|a, b| a.name.cmp(&b.name)),
            CollectionOrder::DateCreated => images.sort_by_cached_key(created_or_modified),
            CollectionOrder::DateModified => images.sort_by_key(|image| image.modified),
            CollectionOrder::FileSize => images.sort_by_key(|image| image.size),
            CollectionOrder::Random => rng.shuffle(&mut images),
        }
        collection.images = images;
        collection.current_index = 0;
    }

    /// Returns the next image in the collection, or `None` once it is exhausted.
    pub fn next_image(&self, collection: &mut CollectionInfo) -> Option<ImageFile> {
        let image = collection.images.get(collection.current_index)?.clone();
        collection.current_index += 1;
        Some(image)
    }
}

fn parse_order(value: &str) -> Option<CollectionOrder> {
    match value.to_ascii_lowercase().as_str() {
        "alphabetical" => Some(CollectionOrder::Alphabetical),
        "datecreated" => Some(CollectionOrder::DateCreated),
        "datemodified" => Some(CollectionOrder::DateModified),
        "filesize" => Some(CollectionOrder::FileSize),
        "random" => Some(CollectionOrder::Random),
        _ => None,
    }
}

fn folder_name(folder: &Path) -> String {
    folder
        .file_name()
        .map(|name| name.to_string_lossy().into_owned())
        .unwrap_or_default()
}

fn clean_name(name: &str) -> String {
    let mut clean = name;
    for prefix in NAMING_PREFIXES {
        if starts_with_ignore_case(clean, prefix) {
            clean = clean[prefix.len()..].trim();
            break;
        }
    }
    for suffix in NAMING_SUFFIXES {
        if ends_with_ignore_case(clean, suffix) {
            clean = clean[..clean.len() - suffix.len()].trim();
            break;
        }
    }
    clean.to_owned()
}

fn has_special_naming(name: &str) -> bool {
    NAMING_PREFIXES
        .iter()
        .any(|prefix| starts_with_ignore_case(name, prefix))
        || NAMING_SUFFIXES
            .iter()
            .any(|suffix| ends_with_ignore_case(name, suffix))
}

fn is_in_collections_folder(folder: &Path) -> bool {
    let mut current = folder;
    while let Some(parent) = current.parent() {
        if parent.file_name().is_some_and(|name| {
            name.to_string_lossy()
                .eq_ignore_ascii_case(COLLECTIONS_DIR_NAME)
        }) {
            return true;
        }
        current = parent;
    }
    false
}

fn starts_with_ignore_case(haystack: &str, needle: &str) -> bool {
    haystack
        .get(..needle.len())
        .is_some_and(|head| head.eq_ignore_ascii_case(needle))
}

fn ends_with_ignore_case(haystack: &str, needle: &str) -> bool {
    haystack
        .get(haystack.len().saturating_sub(needle.len())..)
        .is_some_and(|tail| tail.eq_ignore_ascii_case(needle))
}

fn created_or_modified(image: &ImageFile) -> SystemTime {
    std::fs::metadata(&image.path)
        .and_then(|meta| meta.created())
        .unwrap_or(image.modified)
}

#[cfg(test)]
mod tests {
    use std::path::PathBuf;
    use std::sync::atomic::{AtomicU32, Ordering};

    use super::*;

    static COUNTER: AtomicU32 = AtomicU32::new(0);

    /// Empty temporary directory, removed when the test ends.
    struct TempDir(PathBuf);

    impl TempDir {
        fn new(label: &str) -> Self {
            let unique = COUNTER.fetch_add(1, Ordering::Relaxed);
            let path =
                std::env::temp_dir().join(format!("riv-{label}-{}-{unique}", std::process::id()));
            std::fs::create_dir_all(&path).unwrap();
            Self(path)
        }

        fn path(&self) -> &Path {
            &self.0
        }
    }

    impl Drop for TempDir {
        fn drop(&mut self) {
            let _ = std::fs::remove_dir_all(&self.0);
        }
    }

    #[test]
    fn cleans_prefix_and_suffix() {
        assert_eq!(clean_name("[ALBUM] Holidays"), "Holidays");
        assert_eq!(clean_name("[album] Holidays"), "Holidays");
        assert_eq!(clean_name("Holidays_album"), "Holidays");
        assert_eq!(clean_name("plain"), "plain");
    }

    #[test]
    fn detects_special_naming() {
        let temp = TempDir::new("naming");
        let folder = temp.path().join("[SEQUENCE] Trip");
        std::fs::create_dir_all(&folder).unwrap();
        let collection = CollectionManager::new().detect(&folder).unwrap();
        assert_eq!(collection.name, "Trip");
    }

    #[test]
    fn detects_collection_file_config() {
        let temp = TempDir::new("file");
        let folder = temp.path();
        std::fs::write(
            folder.join(COLLECTION_FILE_NAME),
            r#"{"name":"My Album","order":"fileSize"}"#,
        )
        .unwrap();
        let collection = CollectionManager::new().detect(folder).unwrap();
        assert_eq!(collection.name, "My Album");
        assert_eq!(collection.order, CollectionOrder::FileSize);
    }

    #[test]
    fn detects_collections_folder_parent() {
        let temp = TempDir::new("collections");
        let folder = temp.path().join(COLLECTIONS_DIR_NAME).join("Set One");
        std::fs::create_dir_all(&folder).unwrap();
        let collection = CollectionManager::new().detect(&folder).unwrap();
        assert_eq!(collection.name, "Set One");
    }

    #[test]
    fn plain_folder_is_not_a_collection() {
        let temp = TempDir::new("plain");
        assert!(CollectionManager::new().detect(temp.path()).is_none());
    }

    #[test]
    fn populate_orders_alphabetically() {
        let temp = TempDir::new("order");
        let mut images = Vec::new();
        for name in ["c.png", "a.png", "b.png"] {
            let path = temp.path().join(name);
            std::fs::write(&path, b"x").unwrap();
            images.push(ImageFile::new(path));
        }
        let mut collection = CollectionInfo::new("Set".to_owned());
        let mut rng = Rng::from_seed(1);
        CollectionManager::new().populate(&mut collection, images, &mut rng);
        let names: Vec<_> = collection
            .images
            .iter()
            .map(|image| image.name.as_str())
            .collect();
        assert_eq!(names, ["a.png", "b.png", "c.png"]);
    }
}
