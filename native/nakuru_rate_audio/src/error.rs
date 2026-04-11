use thiserror::Error;

#[derive(Error, Debug)]
pub enum RateAudioError {
    #[error("Null argument: {0}")]
    NullArgument(&'static str),

    #[error("Invalid UTF-8: {0}")]
    InvalidUtf8(&'static str),

    #[error("Invalid argument: {0}")]
    InvalidArgument(String),

    #[error("Unsupported input format: {0}")]
    UnsupportedInputFormat(String),

    #[error("Unsupported output format")]
    #[allow(dead_code)]
    UnsupportedOutputFormat,

    #[error("Decode error: {0}")]
    Decode(String),

    #[error("Stretch error: {0}")]
    Stretch(String),

    #[error("Encode error: {0}")]
    Encode(String),

    #[error("IO error: {0}")]
    Io(#[from] std::io::Error),

    #[error("Dependency unavailable: {0}")]
    DependencyUnavailable(String),

    #[error("Panic crossed FFI boundary")]
    Panic,
}

impl RateAudioError {
    pub fn code(&self) -> i32 {
        match self {
            Self::NullArgument(_) => -1,
            Self::InvalidUtf8(_) => -2,
            Self::InvalidArgument(_) => -3,
            Self::UnsupportedInputFormat(_) => -4,
            Self::UnsupportedOutputFormat => -5,
            Self::Decode(_) => -6,
            Self::Stretch(_) => -7,
            Self::Encode(_) => -8,
            Self::Io(_) => -9,
            Self::DependencyUnavailable(_) => -10,
            Self::Panic => -11,
        }
    }
}

pub type Result<T> = std::result::Result<T, RateAudioError>;
