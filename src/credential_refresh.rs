use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize)]
struct RefreshStartRequest {}

#[derive(Debug, Clone, Deserialize)]
pub struct RefreshStartResponse {
    pub request_token: String,
    pub approve_url: String,
    pub poll_interval_ms: u64,
}

#[derive(Debug, Clone, Deserialize)]
pub struct RefreshStatusResponse {
    pub status: String,
    pub webdav_url: Option<String>,
    pub username: Option<String>,
    pub password: Option<String>,
    pub remote_folder: Option<String>,
    pub credential_profile_id: Option<u64>,
    pub credential_version: Option<u64>,
}

pub fn start_refresh(api_base: &str, device_token: &str) -> Result<RefreshStartResponse, String> {
    let url = format!(
        "{}/api/device/credential-refresh/start",
        api_base.trim_end_matches('/')
    );
    let res = ureq::post(&url)
        .set("Content-Type", "application/json")
        .set("Authorization", &format!("Bearer {device_token}"))
        .send_string(&serde_json::to_string(&RefreshStartRequest {}).map_err(|e| e.to_string())?)
        .map_err(|e| e.to_string())?;
    let body = res.into_string().map_err(|e| e.to_string())?;
    serde_json::from_str(&body).map_err(|e| e.to_string())
}

pub fn poll_refresh(
    api_base: &str,
    request_token: &str,
    device_token: &str,
) -> Result<RefreshStatusResponse, String> {
    let url = format!(
        "{}/api/device/credential-refresh/status/{}",
        api_base.trim_end_matches('/'),
        request_token
    );
    let res = ureq::get(&url)
        .set("Authorization", &format!("Bearer {device_token}"))
        .call()
        .map_err(|e| e.to_string())?;
    let body = res.into_string().map_err(|e| e.to_string())?;
    serde_json::from_str(&body).map_err(|e| e.to_string())
}
