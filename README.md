# Random Image Viewer

A cross-platform (Linux and Windows) desktop app for viewing images in random order from selected folders. Written in Rust with an [egui](https://github.com/emilk/egui) / eframe interface.

## Features

- **Folder selection**: pick a folder and recursively scan for JPEG, PNG, GIF (static and animated), and WebP (static and animated) images.
- **Progressive loading**: display starts as soon as enough images are found, while scanning continues in the background.
- **Random order without repetition**: every image is shown once per cycle, then the pool resets.
- **Navigation history**: step backward and forward through viewed images.
- **Image deletion**: remove unwanted files after a confirmation prompt.
- **Fullscreen mode**: hides all UI and paints only the image.
- **Animated playback**: animated GIF and WebP play with their native frame timing.
- **Image details**: press `I` for the current image's name, folder, size, resolution, format, and modified timestamp (RFC 3339, UTC).
- **Display preferences**: window size, position, and state are remembered between runs.

## Keyboard shortcuts

| Key | Action |
| --- | --- |
| `Space` | Next image (or forward through history) |
| `Backspace` | Previous image |
| `End` / `N` | Jump to the most recent image; in a collection, skip it |
| `Delete` | Delete the current image (with confirmation) |
| `I` | Show details of the current image (name, folder, size, resolution, format, modified) |
| `Enter` / `F11` / `F` | Toggle fullscreen |
| `Ctrl+O` | Open the folder picker |
| `Esc` | Exit fullscreen, or quit when windowed |
| `Q` | Quit |

Double-clicking the image also toggles fullscreen.

## Scaling rules

- Static images scale down to fit the viewing area and are never upscaled.
- Animated GIF and WebP images are shown at their original size, with scrollbars when larger than the window.

## Special collection folders

A folder is treated as an ordered collection (shown sequentially rather than randomly) when any of these apply:

- it contains a `.collection` file;
- it is inside a folder named `_collections`;
- its name starts with `[COLLECTION]`, `[ALBUM]`, or `[SEQUENCE]`, or ends with `_collection`, `_album`, or `_sequence`.

A `.collection` file customises the collection:

```json
{
  "name": "My Photo Album",
  "order": "alphabetical"
}
```

`order` accepts `alphabetical`, `dateCreated`, `dateModified`, `fileSize`, or `random`.

## Requirements

- Rust 1.95 or newer (pinned in `rust-toolchain.toml`).

## Build and run

```sh
cargo run --release
```

## Tests

```sh
cargo test
```

## Packaging (Ubuntu snap)

`packaging/snap/snapcraft.yaml` describes a strictly confined snap.

```sh
sudo snap install snapcraft --classic
sudo snap install lxd && sudo lxd init --minimal
packaging/snap/build-snap.sh
sudo snap install --dangerous packaging/snap/random-image-viewer_*.snap
```

`build-snap.sh` stages the sources, generates the icon from `app.ico`, and runs `snapcraft`, which builds inside the base's container. Building on the host (`snapcraft --destructive-mode`) only works when the host runs the same Ubuntu release as the snap's base (`core24` needs 24.04); a binary built on a newer release links a newer glibc than the base provides.

The `gnome` extension supplies GTK themes, fonts and desktop integration. File access in strict confinement goes through the `home` and `removable-media` interfaces, and the folder picker uses the XDG desktop portal.

## Settings

Window size, maximized state, and the last selected folder are stored in `settings.json` under the platform configuration directory:

- Linux: `~/.config/RandomImageViewer/settings.json`
- Windows: `%APPDATA%\RandomImageViewer\settings.json`

Window position is also stored and restored on Windows, and on Linux it works because the app runs as an X11 (XWayland) client by default when an X display is available — the same approach Electron/Chromium uses. Native Wayland does not expose a window's position to applications, so position cannot be saved there.

The Linux backend can be selected with the `RIV_WINDOW_BACKEND` environment variable:

| `RIV_WINDOW_BACKEND` | Behaviour |
| --- | --- |
| unset (default) | X11/XWayland when `DISPLAY` is set (position remembered), otherwise Wayland |
| `x11` | Force the X11 backend |
| `wayland` | Force native Wayland (no position memory) |

## Project layout

```
src/
├── app.rs        # eframe application: UI, input, dialogs, fullscreen
├── constants.rs  # shared named constants
├── settings.rs   # persisted window/session settings
├── rng.rs        # deterministic PRNG used for shuffling and selection
├── models/       # ImageFile, CollectionInfo
├── services/     # scanning, random selection, collection detection, formatting
└── media/        # image decoding (static) and animation playback
```

The version comes from `Cargo.toml` (`package.version`). `packaging/snap/snapcraft.yaml` repeats it, and `build-snap.sh` refuses to build when the two disagree.
