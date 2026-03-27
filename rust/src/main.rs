// main.rs — entry point
// #![windows_subsystem = "windows"] suppresses the console window on release builds.
#![windows_subsystem = "windows"]

mod config;
mod secret;
mod sync;
mod tray;
mod ui;
mod updater;
mod webdav;

use windows::Win32::System::LibraryLoader::GetModuleHandleW;

fn main() {
    let hinstance = unsafe { GetModuleHandleW(None).unwrap().into() };
    ui::run(hinstance);
}
