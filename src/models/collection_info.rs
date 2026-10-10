use super::image_file::ImageFile;

/// How images in a collection are ordered.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CollectionOrder {
    Alphabetical,
    DateCreated,
    DateModified,
    FileSize,
    Random,
}

/// A folder whose images are shown sequentially instead of randomly.
#[derive(Debug, Clone)]
pub struct CollectionInfo {
    pub name: String,
    pub order: CollectionOrder,
    pub images: Vec<ImageFile>,
    pub current_index: usize,
}

impl CollectionInfo {
    pub fn new(name: String) -> Self {
        Self {
            name,
            order: CollectionOrder::Alphabetical,
            images: Vec::new(),
            current_index: 0,
        }
    }

    pub fn image_count(&self) -> usize {
        self.images.len()
    }
}
