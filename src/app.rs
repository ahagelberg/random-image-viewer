use std::path::PathBuf;
use std::sync::mpsc::{Receiver, TryRecvError};
use std::time::Duration;

use eframe::egui;
#[cfg(target_os = "linux")]
use winit::platform::x11::EventLoopBuilderExtX11;

use crate::constants;
use crate::media::animation::{self, Animation};
use crate::media::decode;
use crate::models::image_file::{ImageFile, ImageFormat};
use crate::services::display;
use crate::services::image_manager::{ImageManager, NextImage, ScanEvent};
use crate::settings::DisplaySettings;

const WELCOME_TEXT: &str = "Select a folder to begin viewing images";
const NO_IMAGE_TEXT: &str = "No image loaded";
const SCANNING_TEXT: &str = "Scanning folder for images...";
const NO_MORE_IMAGES_TEXT: &str = "No more images available";
const LOAD_FAILED_TEXT: &str = "Failed to load image";
const PREVIOUS_EMPTY_TEXT: &str = "No previous images in history";
const FOLDER_DIALOG_TITLE: &str = "Select Folder Containing Images";
const TEXTURE_NAME: &str = "viewer-image";
const ABOUT_FEATURES: &str = "Features:\n\u{2022} Random image display\n\u{2022} Fullscreen mode\n\u{2022} Keyboard navigation\n\u{2022} Animated GIF and WebP support\n\u{2022} Image scaling and history";

const IMAGE_BACKGROUND: egui::Color32 = egui::Color32::BLACK;
const IMAGE_BORDER_COLOR: egui::Color32 = egui::Color32::DARK_GRAY;
const IMAGE_BORDER_WIDTH: f32 = 1.0;
const IMAGE_MARGIN: i8 = 5;
const INFO_WINDOW_WIDTH: f32 = 420.0;
/// Entries after the front one become history when jumping to the end.
const HISTORY_FROM: usize = 1;

#[cfg(target_os = "linux")]
const LINUX_BACKEND_ENV: &str = "RIV_WINDOW_BACKEND";
#[cfg(target_os = "linux")]
const LINUX_BACKEND_X11: &str = "x11";
#[cfg(target_os = "linux")]
const LINUX_BACKEND_WAYLAND: &str = "wayland";
#[cfg(target_os = "linux")]
const DISPLAY_ENV: &str = "DISPLAY";

/// On Linux, default to the X11 (XWayland) backend when an X display exists, so the
/// window position can be saved and restored. Native Wayland never exposes position.
/// Set `RIV_WINDOW_BACKEND=wayland` to force Wayland, or `=x11` to force X11.
#[cfg(target_os = "linux")]
fn prefer_x11() -> bool {
    let requested = std::env::var(LINUX_BACKEND_ENV).ok();
    if requested.as_deref() == Some(LINUX_BACKEND_WAYLAND) {
        return false;
    }
    if requested.as_deref() == Some(LINUX_BACKEND_X11) {
        return true;
    }
    std::env::var_os(DISPLAY_ENV).is_some()
}

pub fn native_options(settings: &DisplaySettings, title: &str) -> eframe::NativeOptions {
    let size = settings.window_size.unwrap_or([
        constants::DEFAULT_WINDOW_WIDTH,
        constants::DEFAULT_WINDOW_HEIGHT,
    ]);
    let mut viewport = egui::ViewportBuilder::default()
        .with_title(title)
        .with_inner_size(size)
        .with_min_inner_size([constants::MIN_WINDOW_WIDTH, constants::MIN_WINDOW_HEIGHT])
        .with_maximized(settings.maximized);
    if let Some(location) = settings.window_location {
        viewport = viewport.with_position(location);
    }
    if let Some(icon) = window_icon() {
        viewport = viewport.with_icon(icon);
    }
    let mut options = eframe::NativeOptions {
        viewport,
        ..Default::default()
    };
    #[cfg(target_os = "linux")]
    if prefer_x11() {
        options.event_loop_builder = Some(Box::new(|builder| {
            builder.with_x11();
        }));
    }
    options
}

fn window_icon() -> Option<egui::IconData> {
    let image =
        image::load_from_memory_with_format(include_bytes!("../app.ico"), image::ImageFormat::Ico)
            .ok()?;
    let rgba = image.to_rgba8();
    Some(egui::IconData {
        rgba: rgba.as_raw().clone(),
        width: rgba.width(),
        height: rgba.height(),
    })
}

struct Displayed {
    file: ImageFile,
    texture: egui::TextureHandle,
    natural: [u32; 2],
    animation: Option<Animation>,
}

pub struct ViewerApp {
    manager: ImageManager,
    scan: Option<Receiver<ScanEvent>>,
    selected_folder: Option<PathBuf>,
    display: Option<Displayed>,
    backward: Vec<ImageFile>,
    forward: Vec<ImageFile>,
    is_fullscreen: bool,
    status: String,
    image_info: String,
    progress: String,
    settings: DisplaySettings,
    show_about: bool,
    show_info: bool,
    confirm_delete: bool,
    error: Option<String>,
}

impl ViewerApp {
    pub fn new(_cc: &eframe::CreationContext<'_>, settings: DisplaySettings) -> Self {
        let selected_folder = settings.last_selected_folder.as_ref().map(PathBuf::from);
        Self {
            manager: ImageManager::new(),
            scan: None,
            selected_folder,
            display: None,
            backward: Vec::new(),
            forward: Vec::new(),
            is_fullscreen: false,
            status: WELCOME_TEXT.to_owned(),
            image_info: NO_IMAGE_TEXT.to_owned(),
            progress: String::new(),
            settings,
            show_info: false,
            show_about: false,
            confirm_delete: false,
            error: None,
        }
    }

    fn poll_scan(&mut self, ctx: &egui::Context) {
        let Some(receiver) = &self.scan else {
            return;
        };
        let mut events = Vec::new();
        let mut disconnected = false;
        loop {
            match receiver.try_recv() {
                Ok(event) => events.push(event),
                Err(TryRecvError::Empty) => break,
                Err(TryRecvError::Disconnected) => {
                    disconnected = true;
                    break;
                }
            }
        }
        for event in events {
            self.apply_scan_event(event, ctx);
        }
        if disconnected {
            self.scan = None;
        }
        if self.scan.is_some() {
            ctx.request_repaint_after(Duration::from_millis(constants::SCAN_POLL_INTERVAL_MS));
        }
    }

    fn apply_scan_event(&mut self, event: ScanEvent, ctx: &egui::Context) {
        match event {
            ScanEvent::Progress(processed) => {
                if !self.is_fullscreen {
                    self.progress = format!("Processing: {processed} files...");
                }
            }
            ScanEvent::Batch {
                images,
                collections,
            } => {
                if self.manager.add_batch(images, collections) {
                    self.status = format!(
                        "Ready to start! Found {} images so far...",
                        self.manager.total_image_count()
                    );
                    self.start_viewing(ctx);
                }
            }
            ScanEvent::Completed => {
                self.manager.finalize_scan();
                self.progress.clear();
                self.status = format!(
                    "Found {} individual images and {} collections",
                    self.manager.total_image_count(),
                    self.manager.total_collection_count()
                );
                self.start_viewing(ctx);
            }
            ScanEvent::Failed(message) => {
                self.status = "Error loading images".to_owned();
                self.error = Some(message);
            }
        }
    }

    fn start_viewing(&mut self, ctx: &egui::Context) {
        if self.display.is_none() && self.manager.has_entries() {
            self.load_next(ctx);
        }
    }

    fn load_next(&mut self, ctx: &egui::Context) {
        let Some(file) = self.next_file() else {
            return;
        };
        self.show_image(ctx, file);
    }

    fn next_file(&mut self) -> Option<ImageFile> {
        if let Some(file) = self.forward.pop() {
            self.push_backward();
            return Some(file);
        }
        self.forward.clear();
        self.push_backward();

        if let Some(next) = self.manager.get_next_image() {
            self.note_collection(&next);
            return Some(next.image);
        }
        self.manager.reset_remaining_images();
        let next = self.manager.get_next_image()?;
        self.note_collection(&next);
        Some(next.image)
    }

    fn load_previous(&mut self, ctx: &egui::Context) {
        let Some(file) = self.backward.pop() else {
            if !self.is_fullscreen {
                self.status = PREVIOUS_EMPTY_TEXT.to_owned();
            }
            return;
        };
        if let Some(current) = self.display.take() {
            self.forward.push(current.file);
        }
        self.show_image(ctx, file);
    }

    fn jump_to_end(&mut self, ctx: &egui::Context) {
        if self.forward.is_empty() {
            if self.manager.is_in_collection() {
                if let Some(notice) = self.manager.skip_current_collection() {
                    self.status = collection_completed_status(&notice.name);
                }
                self.load_next(ctx);
            }
            return;
        }
        self.push_backward();
        // Every entry but the front one becomes history, most recent first.
        self.backward
            .extend(self.forward.drain(HISTORY_FROM..).rev());
        if let Some(end) = self.forward.pop() {
            self.show_image(ctx, end);
        }
    }

    fn push_backward(&mut self) {
        if let Some(current) = self.display.as_ref() {
            self.backward.push(current.file.clone());
        }
    }

    fn note_collection(&mut self, next: &NextImage) {
        if let Some(notice) = &next.collection_completed {
            self.status = collection_completed_status(&notice.name);
        }
        if let Some(notice) = &next.collection_started {
            self.status = format!("Collection: {} ({} images)", notice.name, notice.count);
        }
    }

    fn show_image(&mut self, ctx: &egui::Context, file: ImageFile) {
        match build_display(ctx, &file) {
            Ok(display) => {
                self.image_info = display::image_info(&file, display.natural);
                if !self.is_fullscreen {
                    self.status = format!(
                        "Showing: {} ({} remaining)",
                        file.name,
                        self.manager.remaining_image_count()
                    );
                }
                self.display = Some(display);
            }
            Err(error) => {
                self.display = None;
                self.status = format!("{LOAD_FAILED_TEXT}: {}: {error}", file.name);
                self.image_info = NO_IMAGE_TEXT.to_owned();
            }
        }
    }

    fn delete_current(&mut self, ctx: &egui::Context) {
        let Some(file) = self.display.as_ref().map(|display| display.file.clone()) else {
            return;
        };
        if let Err(error) = std::fs::remove_file(&file.path) {
            self.error = Some(format!("Error deleting image: {error}"));
            return;
        }
        self.manager.remove_image(&file);
        self.backward.retain(|candidate| candidate != &file);
        self.forward.retain(|candidate| candidate != &file);
        self.display = None;
        match self.manager.get_next_image() {
            Some(next) => {
                self.note_collection(&next);
                self.show_image(ctx, next.image);
            }
            None => {
                self.status = NO_MORE_IMAGES_TEXT.to_owned();
                self.image_info = NO_IMAGE_TEXT.to_owned();
            }
        }
    }

    fn open_folder(&mut self) {
        let mut dialog = rfd::FileDialog::new().set_title(FOLDER_DIALOG_TITLE);
        if let Some(folder) = &self.selected_folder {
            dialog = dialog.set_directory(folder);
        }
        if let Some(folder) = dialog.pick_folder() {
            self.start_scan(folder);
        }
    }

    fn start_scan(&mut self, folder: PathBuf) {
        self.settings.last_selected_folder = Some(folder.to_string_lossy().into_owned());
        self.selected_folder = Some(folder.clone());
        self.display = None;
        self.backward.clear();
        self.forward.clear();
        self.status = SCANNING_TEXT.to_owned();
        self.image_info = NO_IMAGE_TEXT.to_owned();
        self.progress.clear();
        self.scan = Some(self.manager.start_scan(folder));
    }

    fn toggle_fullscreen(&mut self, ctx: &egui::Context) {
        self.is_fullscreen = !self.is_fullscreen;
        ctx.send_viewport_cmd(egui::ViewportCommand::Fullscreen(self.is_fullscreen));
    }

    fn advance_animation(&mut self, ctx: &egui::Context) {
        let Some(display) = self.display.as_mut() else {
            return;
        };
        let Some(animation) = display.animation.as_mut() else {
            return;
        };
        let delta = ctx.input(|input| input.stable_dt);
        if animation.advance(Duration::from_secs_f32(delta)) {
            display
                .texture
                .set(animation.frame().clone(), egui::TextureOptions::LINEAR);
        }
        ctx.request_repaint_after(animation.time_to_next_frame());
    }

    fn capture_geometry(&mut self, frame: &eframe::Frame) {
        if self.is_fullscreen {
            return;
        }
        let Some(window) = frame.winit_window() else {
            return;
        };
        let scale = window.scale_factor() as f32;
        self.settings.maximized = window.is_maximized();
        if !self.settings.maximized {
            // Remember the windowed size so unmaximizing restores it.
            let size = window.inner_size();
            self.settings.window_size =
                Some([size.width as f32 / scale, size.height as f32 / scale]);
        }
        // Wayland never reports window position; X11 and Windows do.
        if let Ok(position) = window.outer_position() {
            self.settings.window_location =
                Some([position.x as f32 / scale, position.y as f32 / scale]);
        }
    }

    fn handle_input(&mut self, ctx: &egui::Context) {
        let keys = ctx.input(InputSnapshot::read);
        if keys.info && self.display.is_some() {
            self.show_info = !self.show_info;
        }
        if self.show_about || self.show_info || self.confirm_delete || self.error.is_some() {
            if keys.escape {
                self.show_about = false;
                self.show_info = false;
                self.confirm_delete = false;
                self.error = None;
            }
            return;
        }

        if keys.next && self.manager.has_entries() {
            self.load_next(ctx);
        }
        if keys.previous {
            self.load_previous(ctx);
        }
        if keys.delete {
            self.confirm_delete = self.display.is_some();
        }
        if keys.jump {
            self.jump_to_end(ctx);
        }
        if keys.fullscreen {
            self.toggle_fullscreen(ctx);
        }
        if keys.open {
            self.open_folder();
        }
        if keys.quit {
            ctx.send_viewport_cmd(egui::ViewportCommand::Close);
        }
        if keys.escape {
            if self.is_fullscreen {
                self.toggle_fullscreen(ctx);
            } else {
                ctx.send_viewport_cmd(egui::ViewportCommand::Close);
            }
        }
    }

    fn draw(&mut self, ui: &mut egui::Ui, ctx: &egui::Context) {
        let mut select = false;
        let mut previous = false;
        let mut next = false;
        let mut about = false;
        let mut info = false;

        if !self.is_fullscreen {
            let status = self.status.clone();
            let has_backward = !self.backward.is_empty();
            let has_entries = self.manager.has_entries();
            let has_image = self.display.is_some();
            egui::Panel::top("toolbar").show(ui, |ui| {
                ui.horizontal(|ui| {
                    select = action_button(ui, true, "Select Folder");
                    previous = action_button(ui, has_backward, "Previous (Backspace)");
                    next = action_button(ui, has_entries, "Next (Space)");
                    ui.separator();
                    ui.label(status);
                    ui.separator();
                    ui.label(constants::SHORTCUT_HINT);
                    ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                        about = action_button(ui, true, "About");
                        info = action_button(ui, has_image, "Info (I)");
                    });
                });
            });

            let info_text = self.image_info.clone();
            let progress = self.progress.clone();
            egui::Panel::bottom("status").show(ui, |ui| {
                ui.horizontal(|ui| {
                    ui.label(info_text);
                    ui.separator();
                    ui.label(progress);
                });
            });
        }

        let mut double_clicked = false;
        egui::CentralPanel::default()
            .frame(image_frame(self.is_fullscreen))
            .show(ui, |ui| {
                double_clicked = self.draw_image(ui);
            });

        if self.show_about {
            self.about_window(ctx);
        }
        if self.show_info {
            self.info_window(ctx);
        }
        if self.confirm_delete {
            self.confirm_window(ctx);
        }
        if self.error.is_some() {
            self.error_window(ctx);
        }

        if about {
            self.show_about = true;
        }
        if info {
            self.show_info = true;
        }
        if select {
            self.open_folder();
        }
        if previous {
            self.load_previous(ctx);
        }
        if next {
            self.load_next(ctx);
        }
        if double_clicked {
            self.toggle_fullscreen(ctx);
        }
    }

    fn draw_image(&self, ui: &mut egui::Ui) -> bool {
        let Some(display) = self.display.as_ref() else {
            ui.centered_and_justified(|ui| {
                ui.label(WELCOME_TEXT);
            });
            return false;
        };

        let available = ui.available_size();
        let image_size = if display.animation.is_some() {
            egui::Vec2::new(display.natural[0] as f32, display.natural[1] as f32)
        } else {
            fitted_size(display.natural, available)
        };
        let source = egui::load::SizedTexture::new(display.texture.id(), image_size);
        let image = egui::Image::new(source).sense(egui::Sense::click());

        let response = if image_size.x > available.x || image_size.y > available.y {
            egui::ScrollArea::both()
                .auto_shrink([false, false])
                .show(ui, |ui| ui.add(image))
                .inner
        } else {
            ui.centered_and_justified(|ui| ui.add(image)).inner
        };
        response.double_clicked()
    }

    fn about_window(&mut self, ctx: &egui::Context) {
        egui::Window::new("About")
            .collapsible(false)
            .resizable(false)
            .anchor(egui::Align2::CENTER_CENTER, egui::Vec2::ZERO)
            .show(ctx, |ui| {
                ui.heading(constants::APP_NAME);
                ui.label(format!("Version {}", env!("CARGO_PKG_VERSION")));
                ui.label(env!("CARGO_PKG_DESCRIPTION"));
                ui.label(ABOUT_FEATURES);
                if ui.button("OK").clicked() {
                    self.show_about = false;
                }
            });
    }

    fn info_window(&mut self, ctx: &egui::Context) {
        let details = self
            .display
            .as_ref()
            .map(|display| display::image_details(&display.file, display.natural));
        let Some(details) = details else {
            self.show_info = false;
            return;
        };
        egui::Window::new("Image Info")
            .collapsible(false)
            .resizable(false)
            .anchor(egui::Align2::CENTER_CENTER, egui::Vec2::ZERO)
            .default_width(INFO_WINDOW_WIDTH)
            .show(ctx, |ui| {
                for (label, value) in &details {
                    ui.label(format!("{label}: {value}"));
                }
                if ui.button("OK").clicked() {
                    self.show_info = false;
                }
            });
    }

    fn confirm_window(&mut self, ctx: &egui::Context) {
        let name = self
            .display
            .as_ref()
            .map(|display| display.file.name.clone());
        let mut confirmed = false;
        egui::Window::new("Delete Image")
            .collapsible(false)
            .resizable(false)
            .anchor(egui::Align2::CENTER_CENTER, egui::Vec2::ZERO)
            .show(ctx, |ui| {
                if let Some(name) = &name {
                    ui.label(format!("Are you sure you want to delete '{name}'?"));
                    ui.label("This action cannot be undone.");
                }
                ui.horizontal(|ui| {
                    if ui.button("Yes").clicked() {
                        confirmed = true;
                    }
                    if ui.button("No").clicked() {
                        self.confirm_delete = false;
                    }
                });
            });
        if confirmed {
            self.confirm_delete = false;
            self.delete_current(ctx);
        }
    }

    fn error_window(&mut self, ctx: &egui::Context) {
        let Some(message) = self.error.clone() else {
            return;
        };
        egui::Window::new("Error")
            .collapsible(false)
            .resizable(false)
            .anchor(egui::Align2::CENTER_CENTER, egui::Vec2::ZERO)
            .show(ctx, |ui| {
                ui.label(message);
                if ui.button("OK").clicked() {
                    self.error = None;
                }
            });
    }
}

impl eframe::App for ViewerApp {
    fn ui(&mut self, ui: &mut egui::Ui, frame: &mut eframe::Frame) {
        let ctx = ui.ctx().clone();
        self.poll_scan(&ctx);
        // Captured every frame so the windowed size survives a session that ends
        // in fullscreen, where capture_geometry deliberately skips the update.
        self.capture_geometry(frame);
        if ctx.input(|input| input.viewport().close_requested()) {
            self.settings.save();
        }
        self.handle_input(&ctx);
        self.advance_animation(&ctx);
        self.draw(ui, &ctx);
    }
}

struct InputSnapshot {
    next: bool,
    previous: bool,
    delete: bool,
    jump: bool,
    fullscreen: bool,
    info: bool,
    escape: bool,
    quit: bool,
    open: bool,
}

impl InputSnapshot {
    fn read(input: &egui::InputState) -> Self {
        Self {
            next: input.key_pressed(egui::Key::Space),
            previous: input.key_pressed(egui::Key::Backspace),
            delete: input.key_pressed(egui::Key::Delete),
            jump: input.key_pressed(egui::Key::End) || input.key_pressed(egui::Key::N),
            fullscreen: input.key_pressed(egui::Key::Enter)
                || input.key_pressed(egui::Key::F11)
                || input.key_pressed(egui::Key::F),
            info: input.key_pressed(egui::Key::I),
            escape: input.key_pressed(egui::Key::Escape),
            quit: input.key_pressed(egui::Key::Q),
            open: input.key_pressed(egui::Key::O) && input.modifiers.ctrl,
        }
    }
}

fn action_button(ui: &mut egui::Ui, enabled: bool, label: &str) -> bool {
    let response = ui.add_enabled(enabled, egui::Button::new(label));
    if response.clicked() {
        ui.memory_mut(|memory| memory.surrender_focus(response.id));
    }
    response.clicked()
}

fn image_frame(fullscreen: bool) -> egui::Frame {
    let margin = if fullscreen { 0 } else { IMAGE_MARGIN };
    let frame = egui::Frame::new()
        .fill(IMAGE_BACKGROUND)
        .inner_margin(egui::Margin::same(margin));
    if fullscreen {
        frame
    } else {
        frame.stroke(egui::Stroke::new(IMAGE_BORDER_WIDTH, IMAGE_BORDER_COLOR))
    }
}

/// Fits `natural` inside `available` without ever upscaling.
fn fitted_size(natural: [u32; 2], available: egui::Vec2) -> egui::Vec2 {
    let width = natural[0] as f32;
    let height = natural[1] as f32;
    if width <= available.x && height <= available.y {
        return egui::Vec2::new(width, height);
    }
    let scale = (available.x / width).min(available.y / height);
    egui::Vec2::new(width * scale, height * scale)
}

fn collection_completed_status(name: &str) -> String {
    format!("Collection '{name}' completed. Resuming random display.")
}

fn build_display(ctx: &egui::Context, file: &ImageFile) -> Result<Displayed, String> {
    let animated = match file.format {
        ImageFormat::Gif => true,
        ImageFormat::WebP => animation::is_animated_webp(&file.path),
        _ => false,
    };
    if animated && let Ok(animation) = Animation::load(&file.path) {
        let texture = ctx.load_texture(
            TEXTURE_NAME,
            animation.frame().clone(),
            egui::TextureOptions::LINEAR,
        );
        return Ok(Displayed {
            file: file.clone(),
            texture,
            natural: animation.dimensions(),
            animation: Some(animation),
        });
    }

    let decoded = decode::decode_static(&file.path)?;
    let (color, natural) = decode::to_color_image(&decoded);
    let texture = ctx.load_texture(TEXTURE_NAME, color, egui::TextureOptions::LINEAR);
    Ok(Displayed {
        file: file.clone(),
        texture,
        natural,
        animation: None,
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn small_images_are_not_upscaled() {
        let size = fitted_size([100, 50], egui::Vec2::new(1000.0, 1000.0));
        assert_eq!(size, egui::Vec2::new(100.0, 50.0));
    }

    #[test]
    fn large_images_fit_within_bounds() {
        let size = fitted_size([4000, 2000], egui::Vec2::new(1000.0, 1000.0));
        assert_eq!(size, egui::Vec2::new(1000.0, 500.0));
    }
}
