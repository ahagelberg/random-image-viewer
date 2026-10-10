use std::path::PathBuf;

use serde::{Deserialize, Serialize};

use crate::constants;

/// Persistent window state, stored next to the user's config directory.
#[derive(Debug, Clone, Default, Serialize, Deserialize)]
#[serde(default)]
pub struct DisplaySettings {
    pub window_size: Option<[f32; 2]>,
    pub window_location: Option<[f32; 2]>,
    pub maximized: bool,
    pub last_selected_folder: Option<String>,
}

impl DisplaySettings {
    pub fn file_path() -> Option<PathBuf> {
        dirs::config_dir().map(|dir| {
            dir.join(constants::CONFIG_DIR_NAME)
                .join(constants::SETTINGS_FILE_NAME)
        })
    }

    pub fn load() -> Self {
        let Some(path) = Self::file_path() else {
            return Self::default();
        };
        let Ok(text) = std::fs::read_to_string(&path) else {
            return Self::default();
        };
        serde_json::from_str(&text).unwrap_or_default()
    }

    pub fn save(&self) {
        let Some(path) = Self::file_path() else {
            return;
        };
        let Some(parent) = path.parent() else {
            return;
        };
        if std::fs::create_dir_all(parent).is_err() {
            return;
        }
        if let Ok(text) = serde_json::to_string_pretty(self) {
            let _ = std::fs::write(&path, text);
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn round_trips_through_json() {
        let settings = DisplaySettings {
            window_size: Some([1200.0, 800.0]),
            window_location: Some([10.0, 20.0]),
            maximized: true,
            last_selected_folder: Some("/tmp/pictures".to_owned()),
        };
        let json = serde_json::to_string(&settings).unwrap();
        let parsed: DisplaySettings = serde_json::from_str(&json).unwrap();
        assert_eq!(parsed.window_size, settings.window_size);
        assert_eq!(parsed.window_location, settings.window_location);
        assert_eq!(parsed.maximized, settings.maximized);
        assert_eq!(parsed.last_selected_folder, settings.last_selected_folder);
    }

    #[test]
    fn missing_fields_use_defaults() {
        let parsed: DisplaySettings = serde_json::from_str("{}").unwrap();
        assert!(parsed.window_size.is_none());
        assert!(!parsed.maximized);
    }
}
