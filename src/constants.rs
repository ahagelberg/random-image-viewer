//! Named constants shared across the crate.

/// Display name shown in the window title and About dialog. The crate name is
/// kebab-case (`random-image-viewer`) and unsuitable for the UI.
pub const APP_NAME: &str = "Random Image Viewer";

/// Settings file location under the platform config directory.
pub const CONFIG_DIR_NAME: &str = "RandomImageViewer";
pub const SETTINGS_FILE_NAME: &str = "settings.json";

/// Default and minimum window geometry.
pub const DEFAULT_WINDOW_WIDTH: f32 = 1200.0;
pub const DEFAULT_WINDOW_HEIGHT: f32 = 800.0;
pub const MIN_WINDOW_WIDTH: f32 = 480.0;
pub const MIN_WINDOW_HEIGHT: f32 = 320.0;

/// Interval between scan-channel polls while a scan is running.
pub const SCAN_POLL_INTERVAL_MS: u64 = 30;

/// Toolbar keyboard hint shown to the user.
pub const SHORTCUT_HINT: &str = "Space: Next | Backspace: Previous | End/N: Jump to End | Delete: Remove | I: Info | Enter/F11/F: Fullscreen | Ctrl+O: Open Folder | Esc: Exit | Q: Quit";
