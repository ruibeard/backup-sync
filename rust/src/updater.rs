// updater.rs — check appcast.json on GitHub, download new .exe, replace in place, restart

use serde::Deserialize;

const APPCAST_URL: &str =
    "https://raw.githubusercontent.com/ruibeard/backup-sync-tool/main/appcast.json";

#[derive(Debug, Deserialize)]
struct Appcast {
    version: String,
    #[serde(rename = "downloadUrl")]
    url: String,
}

pub struct UpdateInfo {
    #[allow(dead_code)]
    pub version: String,
    pub url: String,
}

pub fn check(current_version: &str) -> Option<UpdateInfo> {
    let body = ureq::get(APPCAST_URL)
        .timeout(std::time::Duration::from_secs(10))
        .call()
        .ok()?
        .into_string()
        .ok()?;

    let cast: Appcast = serde_json::from_str(&body).ok()?;

    if is_newer(&cast.version, current_version) {
        Some(UpdateInfo {
            version: cast.version,
            url: cast.url,
        })
    } else {
        None
    }
}

pub fn download_and_replace(url: &str, progress: impl Fn(u8)) -> Result<(), String> {
    let resp = ureq::get(url)
        .timeout(std::time::Duration::from_secs(120))
        .call()
        .map_err(|e| e.to_string())?;

    let total = resp
        .header("Content-Length")
        .and_then(|v| v.parse::<u64>().ok())
        .unwrap_or(0);

    let mut reader = resp.into_reader();
    let mut buf = Vec::new();
    let mut downloaded: u64 = 0;
    let mut chunk = [0u8; 65536];

    loop {
        use std::io::Read;
        let n = reader.read(&mut chunk).map_err(|e| e.to_string())?;
        if n == 0 {
            break;
        }
        buf.extend_from_slice(&chunk[..n]);
        downloaded += n as u64;
        if total > 0 {
            progress(((downloaded * 100) / total) as u8);
        }
    }

    // Write to a .tmp file next to the current exe, then swap
    let exe = std::env::current_exe().map_err(|e| e.to_string())?;
    let tmp = exe.with_extension("tmp");
    std::fs::write(&tmp, &buf).map_err(|e| e.to_string())?;

    // On Windows we cannot replace a running exe directly.
    // Write a tiny .bat that waits, copies, and restarts.
    let bat_path = exe.with_extension("update.bat");
    let bat = format!(
        "@echo off\r\ntimeout /t 2 /nobreak >nul\r\nmove /y \"{tmp}\" \"{exe}\"\r\nstart \"\" \"{exe}\"\r\ndel \"%~f0\"\r\n",
        tmp = tmp.display(),
        exe = exe.display(),
    );
    std::fs::write(&bat_path, bat).map_err(|e| e.to_string())?;

    // Launch the bat and exit
    std::process::Command::new("cmd")
        .args(["/c", &bat_path.to_string_lossy()])
        .spawn()
        .map_err(|e| e.to_string())?;

    std::process::exit(0);
}

fn is_newer(candidate: &str, current: &str) -> bool {
    fn parse(v: &str) -> (u32, u32, u32) {
        let mut parts = v.trim_start_matches('v').splitn(3, '.');
        let major = parts.next().and_then(|s| s.parse().ok()).unwrap_or(0);
        let minor = parts.next().and_then(|s| s.parse().ok()).unwrap_or(0);
        let patch = parts.next().and_then(|s| s.parse().ok()).unwrap_or(0);
        (major, minor, patch)
    }
    parse(candidate) > parse(current)
}
