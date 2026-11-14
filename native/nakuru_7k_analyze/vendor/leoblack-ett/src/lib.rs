//! Stand-in for mania-hub's `leoblack-ett` (MinaCalc compiled to WebAssembly).
//!
//! NakuruTool only analyzes native 7K charts, which mania-hub rates with Prism:
//! `chart-pipeline`'s `compute_msd` calls MinaCalc only for key counts outside
//! Prism's 6K-8K range. This crate keeps the items `chart-pipeline` refers to,
//! with the same signatures, and fails every MinaCalc request, so no MinaCalc
//! build and no WebAssembly runtime is linked. See UPSTREAM.md.

pub use leoblack_parser::{OsuFileParser, ParseStatus};

pub const DEFAULT_SCORE_GOAL: f64 = 0.93;

pub const DISPLAY_SKILLSET_ORDER: [&str; 8] =
    ["Stream", "Jumpstream", "Handstream", "Stamina", "JackSpeed", "Chordjack", "Technical", "Overall"];

/// The upstream MinaCalc builds; only `key` is used by `chart-pipeline`.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub enum EttVersion {
    V0723,
    V0740,
    V0750,
}

impl EttVersion {
    pub fn key(self) -> &'static str {
        match self {
            EttVersion::V0723 => "0.72.3",
            EttVersion::V0740 => "0.74.0",
            EttVersion::V0750 => "0.75.0",
        }
    }
}

#[derive(Debug, Clone)]
pub struct EttOptions {
    pub music_rate: f64,
    pub score_goal: f64,
    pub key_override: Option<f64>,
    pub cvt_flag: Option<String>,
    pub etterna_version: Option<String>,
    pub ln_tail_taps: bool,
}

impl Default for EttOptions {
    fn default() -> Self {
        EttOptions {
            music_rate: 1.0,
            score_goal: DEFAULT_SCORE_GOAL,
            key_override: None,
            cvt_flag: None,
            etterna_version: None,
            ln_tail_taps: false,
        }
    }
}

#[derive(Debug, Clone, PartialEq)]
pub struct EttResult {
    pub keycount: i32,
    pub ln_ratio: f64,
    pub etterna_version: Option<EttVersion>,
    pub values: [f64; 8],
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct EttError {
    pub message: String,
}

impl std::fmt::Display for EttError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.write_str(&self.message)
    }
}

impl std::error::Error for EttError {}

/// Always fails: MinaCalc is not part of this build.
pub fn analyze_etterna_from_parsed(_chart: OsuFileParser, _options: &EttOptions) -> Result<EttResult, EttError> {
    Err(EttError { message: "MinaCalc is not included in this build (7K charts use Prism)".to_string() })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn minacalc_requests_fail() {
        let chart = OsuFileParser::parse("osu file format v14\n");
        assert!(analyze_etterna_from_parsed(chart, &EttOptions::default()).is_err());
    }
}
