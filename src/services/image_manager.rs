use std::collections::HashMap;
use std::path::{Path, PathBuf};
use std::sync::mpsc::{self, Receiver, Sender};

use rayon::prelude::*;
use walkdir::WalkDir;

use crate::media::decode;
use crate::models::collection_info::CollectionInfo;
use crate::models::image_file::ImageFile;
use crate::rng::Rng;
use crate::services::collection_manager::CollectionManager;

/// Entries needed before progressive display starts.
const MIN_ENTRIES_TO_START: usize = 20;
/// Directories handled per progressive batch.
const SCAN_BATCH_DIRS: usize = 25;
/// Reshuffle the remaining entries after this many new entries.
const RESHUFFLE_STEP: usize = 25;

/// Progress messages streamed from the scanning thread.
pub enum ScanEvent {
    Progress(usize),
    Batch {
        images: Vec<ImageFile>,
        collections: Vec<CollectionInfo>,
    },
    Completed,
    Failed(String),
}

pub struct CollectionNotice {
    pub name: String,
    pub count: usize,
}

/// The next image to display, plus any collection transitions it caused.
pub struct NextImage {
    pub image: ImageFile,
    pub collection_started: Option<CollectionNotice>,
    pub collection_completed: Option<CollectionNotice>,
}

/// Owns every discovered image/collection plus the random selection state.
///
/// The `images`/`collections` vectors own the entries and the `remaining_*`
/// vectors hold indices into them, so an entry is stored once no matter how
/// often the pool restarts.
pub struct ImageManager {
    images: Vec<ImageFile>,
    remaining_images: Vec<usize>,
    collections: Vec<CollectionInfo>,
    remaining_collections: Vec<usize>,
    current_collection: Option<usize>,
    collection_manager: CollectionManager,
    rng: Rng,
    ready: bool,
    last_reshuffle: usize,
}

impl ImageManager {
    pub fn new() -> Self {
        Self {
            images: Vec::new(),
            remaining_images: Vec::new(),
            collections: Vec::new(),
            remaining_collections: Vec::new(),
            current_collection: None,
            collection_manager: CollectionManager::new(),
            rng: Rng::from_clock(),
            ready: false,
            last_reshuffle: 0,
        }
    }

    pub fn total_image_count(&self) -> usize {
        self.images.len()
    }

    pub fn total_collection_count(&self) -> usize {
        self.collections.len()
    }

    pub fn remaining_image_count(&self) -> usize {
        self.remaining_images.len()
    }

    /// True once the scan has produced any image or collection.
    pub fn has_entries(&self) -> bool {
        !self.images.is_empty() || !self.collections.is_empty()
    }

    pub fn is_in_collection(&self) -> bool {
        self.current_collection.is_some()
    }

    /// Clears all state and starts scanning `root` on a background thread.
    pub fn start_scan(&mut self, root: PathBuf) -> Receiver<ScanEvent> {
        self.clear();
        let (sender, receiver) = mpsc::channel();
        std::thread::spawn(move || scan_folder(root, &sender));
        receiver
    }

    /// Appends a scanned batch; returns whether progressive display may start.
    pub fn add_batch(&mut self, images: Vec<ImageFile>, collections: Vec<CollectionInfo>) -> bool {
        let first_image = self.images.len();
        self.images.extend(images);
        self.remaining_images.extend(first_image..self.images.len());

        let first_collection = self.collections.len();
        self.collections.extend(collections);
        self.remaining_collections
            .extend(first_collection..self.collections.len());

        let total = self.total_entries();
        if !self.ready {
            if total >= MIN_ENTRIES_TO_START {
                self.ready = true;
                self.shuffle_remaining();
                self.last_reshuffle = total;
                return true;
            }
            return false;
        }
        if total >= self.last_reshuffle + RESHUFFLE_STEP {
            self.shuffle_remaining();
            self.last_reshuffle = total;
        }
        false
    }

    /// Final shuffle once scanning has finished.
    pub fn finalize_scan(&mut self) {
        let total = self.total_entries();
        if total == 0 {
            return;
        }
        self.ready = true;
        self.shuffle_remaining();
        self.last_reshuffle = total;
    }

    /// Picks the next image, draining an active collection first.
    pub fn get_next_image(&mut self) -> Option<NextImage> {
        let mut collection_completed = None;

        if let Some(index) = self.current_collection {
            let collection = &mut self.collections[index];
            if let Some(image) = self.collection_manager.next_image(collection) {
                return Some(NextImage {
                    image,
                    collection_started: None,
                    collection_completed: None,
                });
            }
            collection_completed = Some(CollectionNotice {
                name: collection.name.clone(),
                count: collection.image_count(),
            });
            self.current_collection = None;
        }

        let total = self.remaining_images.len() + self.remaining_collections.len();
        if total == 0 {
            return None;
        }

        let index = self.rng.below(total);
        if index < self.remaining_images.len() {
            let slot = self.remaining_images.swap_remove(index);
            return Some(NextImage {
                image: self.images[slot].clone(),
                collection_started: None,
                collection_completed,
            });
        }

        let collection_index = index - self.remaining_images.len();
        let slot = self.remaining_collections.swap_remove(collection_index);
        let collection = &mut self.collections[slot];
        let image = self.collection_manager.next_image(collection)?;
        let collection_started = Some(CollectionNotice {
            name: collection.name.clone(),
            count: collection.image_count(),
        });
        self.current_collection = Some(slot);
        Some(NextImage {
            image,
            collection_started,
            collection_completed,
        })
    }

    pub fn remove_image(&mut self, image: &ImageFile) {
        let Some(position) = self.images.iter().position(|candidate| candidate == image) else {
            return;
        };
        let last = self.images.len() - 1;
        self.remaining_images.retain(|slot| *slot != position);
        self.images.swap_remove(position);
        // The entry that was last now sits at `position`; repoint its slot.
        if position != last
            && let Some(slot) = self.remaining_images.iter_mut().find(|slot| **slot == last)
        {
            *slot = position;
        }
    }

    pub fn skip_current_collection(&mut self) -> Option<CollectionNotice> {
        let index = self.current_collection.take()?;
        let collection = &self.collections[index];
        Some(CollectionNotice {
            name: collection.name.clone(),
            count: collection.image_count(),
        })
    }

    pub fn reset_remaining_images(&mut self) {
        self.remaining_images = (0..self.images.len()).collect();
        self.remaining_collections = (0..self.collections.len()).collect();
        for collection in &mut self.collections {
            collection.current_index = 0;
        }
        self.shuffle_remaining();
        self.current_collection = None;
    }

    fn clear(&mut self) {
        self.images.clear();
        self.remaining_images.clear();
        self.collections.clear();
        self.remaining_collections.clear();
        self.current_collection = None;
        self.ready = false;
        self.last_reshuffle = 0;
    }

    fn total_entries(&self) -> usize {
        self.images.len() + self.collections.len()
    }

    fn shuffle_remaining(&mut self) {
        self.rng.shuffle(&mut self.remaining_images);
        self.rng.shuffle(&mut self.remaining_collections);
    }
}

enum BuiltDirectory {
    Images(Vec<ImageFile>),
    Collection(Box<CollectionInfo>),
}

fn scan_folder(root: PathBuf, sender: &Sender<ScanEvent>) {
    if !root.is_dir() {
        let _ = sender.send(ScanEvent::Failed(format!(
            "Folder not found: {}",
            root.display()
        )));
        return;
    }

    let groups = group_by_directory(discover_images(&root));
    let mut processed = 0;

    for batch in groups.chunks(SCAN_BATCH_DIRS) {
        let built: Vec<BuiltDirectory> = batch
            .par_iter()
            .map(|(dir, paths)| build_directory(dir, paths))
            .collect();

        let mut images = Vec::new();
        let mut collections = Vec::new();
        for entry in built {
            match entry {
                BuiltDirectory::Images(list) => {
                    processed += list.len();
                    images.extend(list);
                }
                BuiltDirectory::Collection(collection) => {
                    processed += collection.image_count();
                    collections.push(*collection);
                }
            }
        }
        if sender.send(ScanEvent::Progress(processed)).is_err() {
            return;
        }
        if sender
            .send(ScanEvent::Batch {
                images,
                collections,
            })
            .is_err()
        {
            return;
        }
    }

    let _ = sender.send(ScanEvent::Completed);
}

fn discover_images(root: &Path) -> Vec<PathBuf> {
    WalkDir::new(root)
        .follow_links(false)
        .into_iter()
        .filter_map(Result::ok)
        .filter(|entry| entry.file_type().is_file())
        .map(walkdir::DirEntry::into_path)
        .filter(|path| decode::is_supported(path))
        .collect()
}

fn group_by_directory(files: Vec<PathBuf>) -> Vec<(PathBuf, Vec<PathBuf>)> {
    let mut groups: HashMap<PathBuf, Vec<PathBuf>> = HashMap::new();
    for file in files {
        if let Some(parent) = file.parent() {
            groups.entry(parent.to_path_buf()).or_default().push(file);
        }
    }
    groups.into_iter().collect()
}

fn build_directory(dir: &Path, paths: &[PathBuf]) -> BuiltDirectory {
    let images: Vec<ImageFile> = paths.iter().cloned().map(ImageFile::new).collect();
    let manager = CollectionManager::new();
    match manager.detect(dir) {
        Some(mut collection) => {
            let mut rng = Rng::from_clock();
            manager.populate(&mut collection, images, &mut rng);
            BuiltDirectory::Collection(Box::new(collection))
        }
        None => BuiltDirectory::Images(images),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn image_file(name: &str) -> ImageFile {
        ImageFile::new(PathBuf::from(format!("/nonexistent/{name}")))
    }

    #[test]
    fn yields_every_image_once_then_exhausts() {
        let mut manager = ImageManager::new();
        let images = vec![
            image_file("a.png"),
            image_file("b.png"),
            image_file("c.png"),
        ];
        manager.add_batch(images, Vec::new());

        let mut seen = Vec::new();
        while let Some(next) = manager.get_next_image() {
            seen.push(next.image.name);
        }
        seen.sort();
        assert_eq!(seen, ["a.png", "b.png", "c.png"]);

        manager.reset_remaining_images();
        assert!(manager.get_next_image().is_some());
    }

    #[test]
    fn plays_collection_in_order_then_completes() {
        let mut manager = ImageManager::new();
        let mut collection = CollectionInfo::new("Set".to_owned());
        collection.images = vec![image_file("1.png"), image_file("2.png")];
        manager.add_batch(Vec::new(), vec![collection]);

        let first = manager.get_next_image().unwrap();
        assert_eq!(first.image.name, "1.png");
        assert!(first.collection_started.is_some());
        assert!(manager.is_in_collection());

        let second = manager.get_next_image().unwrap();
        assert_eq!(second.image.name, "2.png");
        assert!(second.collection_completed.is_none());

        assert!(manager.get_next_image().is_none());
        assert!(!manager.is_in_collection());
    }

    #[test]
    fn remove_image_drops_it_from_the_pool() {
        let mut manager = ImageManager::new();
        let images = vec![
            image_file("a.png"),
            image_file("b.png"),
            image_file("c.png"),
        ];
        manager.add_batch(images, Vec::new());
        manager.remove_image(&image_file("b.png"));
        manager.remove_image(&image_file("c.png"));
        assert_eq!(manager.total_image_count(), 1);

        let remaining: Vec<_> = std::iter::from_fn(|| manager.get_next_image())
            .map(|next| next.image.name)
            .collect();
        assert_eq!(remaining, ["a.png"]);

        manager.reset_remaining_images();
        assert_eq!(manager.get_next_image().unwrap().image.name, "a.png");
        assert!(manager.get_next_image().is_none());
    }

    #[test]
    fn reports_missing_folder() {
        let mut manager = ImageManager::new();
        let receiver = manager.start_scan(PathBuf::from("/definitely/not/here"));
        match receiver.recv() {
            Ok(ScanEvent::Failed(message)) => assert!(message.contains("Folder not found")),
            _ => panic!("expected a Failed event"),
        }
    }
}
