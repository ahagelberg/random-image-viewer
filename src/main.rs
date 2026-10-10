#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod app;
mod constants;
mod media;
mod models;
mod rng;
mod services;
mod settings;

use app::{ViewerApp, native_options};
use settings::DisplaySettings;

fn main() -> eframe::Result<()> {
    let settings = DisplaySettings::load();
    let title = format!("{} v{}", constants::APP_NAME, env!("CARGO_PKG_VERSION"));
    let options = native_options(&settings, &title);
    eframe::run_native(
        &title,
        options,
        Box::new(|cc| Ok(Box::new(ViewerApp::new(cc, settings)))),
    )
}
