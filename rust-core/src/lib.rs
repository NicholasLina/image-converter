use std::ffi::{CStr, CString};
use std::fs;
use std::io::Cursor;
use std::os::raw::c_char;
use std::path::Path;

use image::codecs::avif::AvifEncoder;
use image::codecs::bmp::BmpEncoder;
use image::codecs::gif::GifEncoder;
use image::codecs::jpeg::JpegEncoder;
use image::codecs::png::{CompressionType, FilterType, PngEncoder};
use image::codecs::tiff::TiffEncoder;
use image::codecs::webp::WebPEncoder;
use image::{DynamicImage, ExtendedColorType, Frame, ImageEncoder};

#[repr(i32)]
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum OutputFormat {
    Jpeg = 0,
    Png = 1,
    WebP = 2,
    Avif = 3,
    Tiff = 4,
    Bmp = 5,
    Gif = 6,
}

impl OutputFormat {
    pub fn extension(self) -> &'static str {
        match self {
            Self::Jpeg => "jpg",
            Self::Png => "png",
            Self::WebP => "webp",
            Self::Avif => "avif",
            Self::Tiff => "tiff",
            Self::Bmp => "bmp",
            Self::Gif => "gif",
        }
    }

    pub fn supports_quality(self) -> bool {
        matches!(self, Self::Jpeg | Self::Avif)
    }
}

impl TryFrom<i32> for OutputFormat {
    type Error = String;

    fn try_from(value: i32) -> Result<Self, Self::Error> {
        match value {
            0 => Ok(Self::Jpeg),
            1 => Ok(Self::Png),
            2 => Ok(Self::WebP),
            3 => Ok(Self::Avif),
            4 => Ok(Self::Tiff),
            5 => Ok(Self::Bmp),
            6 => Ok(Self::Gif),
            _ => Err(format!("Unsupported output format value: {value}")),
        }
    }
}

fn normalized_quality(quality: u8) -> u8 {
    quality.clamp(1, 100)
}

fn encode_image(
    image: &DynamicImage,
    output_format: OutputFormat,
    quality: u8,
) -> Result<Vec<u8>, String> {
    let mut bytes = Vec::<u8>::new();
    let quality = normalized_quality(quality);

    match output_format {
        OutputFormat::Jpeg => {
            let rgb8 = image.to_rgb8();
            let (width, height) = rgb8.dimensions();
            let mut encoder = JpegEncoder::new_with_quality(&mut bytes, quality);
            encoder
                .encode(&rgb8, width, height, ExtendedColorType::Rgb8)
                .map_err(|e| format!("JPEG encode failed: {e}"))?;
        }
        OutputFormat::Png => {
            let rgba8 = image.to_rgba8();
            let (width, height) = rgba8.dimensions();
            let encoder =
                PngEncoder::new_with_quality(&mut bytes, CompressionType::Best, FilterType::Adaptive);
            encoder
                .write_image(&rgba8, width, height, ExtendedColorType::Rgba8)
                .map_err(|e| format!("PNG encode failed: {e}"))?;
        }
        OutputFormat::WebP => {
            let rgba8 = image.to_rgba8();
            let (width, height) = rgba8.dimensions();
            let encoder = WebPEncoder::new_lossless(&mut bytes);
            encoder
                .write_image(&rgba8, width, height, ExtendedColorType::Rgba8)
                .map_err(|e| format!("WebP encode failed: {e}"))?;
        }
        OutputFormat::Avif => {
            let rgba8 = image.to_rgba8();
            let (width, height) = rgba8.dimensions();
            let encoder = AvifEncoder::new_with_speed_quality(&mut bytes, 6, quality);
            encoder
                .write_image(&rgba8, width, height, ExtendedColorType::Rgba8)
                .map_err(|e| format!("AVIF encode failed: {e}"))?;
        }
        OutputFormat::Tiff => {
            let rgba8 = image.to_rgba8();
            let (width, height) = rgba8.dimensions();
            let mut cursor = Cursor::new(Vec::new());
            let encoder = TiffEncoder::new(&mut cursor);
            encoder
                .encode(&rgba8, width, height, ExtendedColorType::Rgba8)
                .map_err(|e| format!("TIFF encode failed: {e}"))?;
            bytes = cursor.into_inner();
        }
        OutputFormat::Bmp => {
            let rgba8 = image.to_rgba8();
            let (width, height) = rgba8.dimensions();
            let mut encoder = BmpEncoder::new(&mut bytes);
            encoder
                .encode(&rgba8, width, height, ExtendedColorType::Rgba8)
                .map_err(|e| format!("BMP encode failed: {e}"))?;
        }
        OutputFormat::Gif => {
            let rgba8 = image.to_rgba8();
            let frame = Frame::new(rgba8);
            let mut encoder = GifEncoder::new(&mut bytes);
            encoder
                .encode_frame(frame)
                .map_err(|e| format!("GIF encode failed: {e}"))?;
        }
    }

    Ok(bytes)
}

pub fn convert_image(
    input_path: &Path,
    output_path: &Path,
    output_format: OutputFormat,
    quality: u8,
) -> Result<(), String> {
    let image =
        image::open(input_path).map_err(|e| format!("Unable to open {}: {e}", input_path.display()))?;
    let encoded = encode_image(&image, output_format, quality)?;

    if let Some(parent) = output_path.parent() {
        if !parent.as_os_str().is_empty() {
            fs::create_dir_all(parent)
                .map_err(|e| format!("Unable to create {}: {e}", parent.display()))?;
        }
    }

    fs::write(output_path, encoded)
        .map_err(|e| format!("Unable to write {}: {e}", output_path.display()))?;
    Ok(())
}

pub fn estimate_output_size(
    input_path: &Path,
    output_format: OutputFormat,
    quality: u8,
) -> Result<u64, String> {
    let image =
        image::open(input_path).map_err(|e| format!("Unable to open {}: {e}", input_path.display()))?;
    let encoded = encode_image(&image, output_format, quality)?;
    Ok(encoded.len() as u64)
}

fn read_c_str(ptr: *const c_char, field_name: &str) -> Result<String, String> {
    if ptr.is_null() {
        return Err(format!("{field_name} pointer was null"));
    }

    let c_str = unsafe { CStr::from_ptr(ptr) };
    c_str
        .to_str()
        .map(|s| s.to_owned())
        .map_err(|_| format!("{field_name} was not valid UTF-8"))
}

unsafe fn write_error(error_out: *mut *mut c_char, message: String) {
    if error_out.is_null() {
        return;
    }

    let sanitized = message.replace('\0', " ");
    let error = CString::new(sanitized).unwrap_or_else(|_| {
        CString::new("Image conversion failed with an unknown error.")
            .expect("fallback message never contains NUL")
    });
    *error_out = error.into_raw();
}

#[no_mangle]
pub unsafe extern "C" fn convert_image_ffi(
    input_path: *const c_char,
    output_path: *const c_char,
    output_format: i32,
    quality: u8,
    error_out: *mut *mut c_char,
) -> bool {
    if !error_out.is_null() {
        *error_out = std::ptr::null_mut();
    }

    let operation = || -> Result<(), String> {
        let input_path = read_c_str(input_path, "input_path")?;
        let output_path = read_c_str(output_path, "output_path")?;
        let output_format = OutputFormat::try_from(output_format)?;

        convert_image(
            Path::new(&input_path),
            Path::new(&output_path),
            output_format,
            quality,
        )
    };

    match operation() {
        Ok(()) => true,
        Err(message) => {
            write_error(error_out, message);
            false
        }
    }
}

#[no_mangle]
pub unsafe extern "C" fn estimate_output_size_ffi(
    input_path: *const c_char,
    output_format: i32,
    quality: u8,
) -> i64 {
    let operation = || -> Result<i64, String> {
        let input_path = read_c_str(input_path, "input_path")?;
        let output_format = OutputFormat::try_from(output_format)?;
        let bytes = estimate_output_size(Path::new(&input_path), output_format, quality)?;
        i64::try_from(bytes).map_err(|_| "Estimated size exceeded i64 range".to_string())
    };

    operation().unwrap_or(-1)
}

#[no_mangle]
pub unsafe extern "C" fn free_rust_string(ptr: *mut c_char) {
    if ptr.is_null() {
        return;
    }

    drop(CString::from_raw(ptr));
}

#[cfg(test)]
mod tests {
    use std::path::Path;

    use image::{DynamicImage, ImageBuffer, ImageFormat, Rgba};
    use tempfile::tempdir;

    use super::{convert_image, estimate_output_size, normalized_quality, OutputFormat};

    fn create_source_image(path: &Path) {
        let source = ImageBuffer::from_fn(200, 140, |x, y| {
            Rgba([
                (x % 255) as u8,
                (y % 255) as u8,
                ((x.wrapping_mul(y)) % 255) as u8,
                255,
            ])
        });

        DynamicImage::ImageRgba8(source)
            .save_with_format(path, ImageFormat::Png)
            .expect("failed to create source image");
    }

    fn create_small_image(path: &Path) {
        let source = ImageBuffer::from_fn(10, 10, |x, y| {
            Rgba([(x * 25) as u8, (y * 25) as u8, 128, 255])
        });

        DynamicImage::ImageRgba8(source)
            .save_with_format(path, ImageFormat::Png)
            .expect("failed to create small image");
    }

    #[test]
    fn converts_image_to_jpeg() {
        let temp = tempdir().expect("tempdir");
        let source = temp.path().join("source.png");
        let output = temp.path().join("converted.jpg");
        create_source_image(&source);

        convert_image(&source, &output, OutputFormat::Jpeg, 85).expect("conversion should succeed");

        assert!(output.exists(), "output file should exist after conversion");
        let reopened = image::open(&output).expect("output should be readable image");
        assert_eq!(reopened.width(), 200);
        assert_eq!(reopened.height(), 140);
    }

    #[test]
    fn converts_image_to_png() {
        let temp = tempdir().expect("tempdir");
        let source = temp.path().join("source.png");
        let output = temp.path().join("converted.png");
        create_source_image(&source);

        convert_image(&source, &output, OutputFormat::Png, 85).expect("conversion should succeed");

        assert!(output.exists(), "output file should exist after conversion");
        let reopened = image::open(&output).expect("output should be readable image");
        assert_eq!(reopened.width(), 200);
        assert_eq!(reopened.height(), 140);
    }

    #[test]
    fn converts_image_to_webp() {
        let temp = tempdir().expect("tempdir");
        let source = temp.path().join("source.png");
        let output = temp.path().join("converted.webp");
        create_source_image(&source);

        convert_image(&source, &output, OutputFormat::WebP, 85).expect("conversion should succeed");

        assert!(output.exists(), "output file should exist after conversion");
    }

    #[test]
    fn converts_image_to_avif() {
        let temp = tempdir().expect("tempdir");
        let source = temp.path().join("source.png");
        let output = temp.path().join("converted.avif");
        create_small_image(&source);

        convert_image(&source, &output, OutputFormat::Avif, 85).expect("conversion should succeed");

        assert!(output.exists(), "output file should exist after conversion");
    }

    #[test]
    fn converts_image_to_tiff() {
        let temp = tempdir().expect("tempdir");
        let source = temp.path().join("source.png");
        let output = temp.path().join("converted.tiff");
        create_source_image(&source);

        convert_image(&source, &output, OutputFormat::Tiff, 85).expect("conversion should succeed");

        assert!(output.exists(), "output file should exist after conversion");
    }

    #[test]
    fn converts_image_to_bmp() {
        let temp = tempdir().expect("tempdir");
        let source = temp.path().join("source.png");
        let output = temp.path().join("converted.bmp");
        create_source_image(&source);

        convert_image(&source, &output, OutputFormat::Bmp, 85).expect("conversion should succeed");

        assert!(output.exists(), "output file should exist after conversion");
    }

    #[test]
    fn converts_image_to_gif() {
        let temp = tempdir().expect("tempdir");
        let source = temp.path().join("source.png");
        let output = temp.path().join("converted.gif");
        create_source_image(&source);

        convert_image(&source, &output, OutputFormat::Gif, 85).expect("conversion should succeed");

        assert!(output.exists(), "output file should exist after conversion");
    }

    #[test]
    fn quality_changes_jpeg_estimate() {
        let temp = tempdir().expect("tempdir");
        let source = temp.path().join("source.png");
        create_source_image(&source);

        let low_quality =
            estimate_output_size(&source, OutputFormat::Jpeg, 20).expect("estimate should succeed");
        let high_quality =
            estimate_output_size(&source, OutputFormat::Jpeg, 90).expect("estimate should succeed");

        assert!(high_quality > low_quality, "higher quality should increase output size");
    }

    #[test]
    fn quality_changes_avif_estimate() {
        let temp = tempdir().expect("tempdir");
        let source = temp.path().join("source.png");
        create_small_image(&source);

        let low_quality =
            estimate_output_size(&source, OutputFormat::Avif, 20).expect("estimate should succeed");
        let high_quality =
            estimate_output_size(&source, OutputFormat::Avif, 90).expect("estimate should succeed");

        assert!(high_quality > low_quality, "higher quality should increase output size for AVIF");
    }

    #[test]
    fn estimates_size_for_lossless_format() {
        let temp = tempdir().expect("tempdir");
        let source = temp.path().join("source.png");
        create_source_image(&source);

        let estimate =
            estimate_output_size(&source, OutputFormat::WebP, 50).expect("estimate should succeed");
        assert!(estimate > 0, "estimate should be positive");
    }

    #[test]
    fn estimate_matches_actual_file_size() {
        let temp = tempdir().expect("tempdir");
        let source = temp.path().join("source.png");
        let output = temp.path().join("converted.jpg");
        create_source_image(&source);

        let estimate = estimate_output_size(&source, OutputFormat::Jpeg, 85)
            .expect("estimate should succeed");
        convert_image(&source, &output, OutputFormat::Jpeg, 85).expect("conversion should succeed");

        let actual_size = std::fs::metadata(&output).expect("output file should exist").len();
        assert_eq!(estimate, actual_size, "estimate should match actual file size");
    }

    #[test]
    fn converts_with_subdirectory_creation() {
        let temp = tempdir().expect("tempdir");
        let source = temp.path().join("source.png");
        let output = temp.path().join("subdir").join("nested").join("converted.jpg");
        create_source_image(&source);

        convert_image(&source, &output, OutputFormat::Jpeg, 85).expect("conversion should succeed");

        assert!(output.exists(), "output file should exist in nested directory");
    }

    #[test]
    fn fails_on_nonexistent_input() {
        let temp = tempdir().expect("tempdir");
        let source = temp.path().join("nonexistent.png");
        let output = temp.path().join("output.jpg");

        let result = convert_image(&source, &output, OutputFormat::Jpeg, 85);
        assert!(result.is_err(), "should fail for nonexistent input");
    }

    #[test]
    fn estimate_fails_on_nonexistent_input() {
        let temp = tempdir().expect("tempdir");
        let source = temp.path().join("nonexistent.png");

        let result = estimate_output_size(&source, OutputFormat::Jpeg, 85);
        assert!(result.is_err(), "should fail for nonexistent input");
    }

    #[test]
    fn normalized_quality_clamps_to_valid_range() {
        assert_eq!(normalized_quality(0), 1, "quality 0 should clamp to 1");
        assert_eq!(normalized_quality(1), 1, "quality 1 should remain 1");
        assert_eq!(normalized_quality(50), 50, "quality 50 should remain 50");
        assert_eq!(normalized_quality(100), 100, "quality 100 should remain 100");
        assert_eq!(normalized_quality(150), 100, "quality 150 should clamp to 100");
        assert_eq!(normalized_quality(255), 100, "quality 255 should clamp to 100");
    }

    #[test]
    fn output_format_try_from_valid_values() {
        assert_eq!(OutputFormat::try_from(0), Ok(OutputFormat::Jpeg));
        assert_eq!(OutputFormat::try_from(1), Ok(OutputFormat::Png));
        assert_eq!(OutputFormat::try_from(2), Ok(OutputFormat::WebP));
        assert_eq!(OutputFormat::try_from(3), Ok(OutputFormat::Avif));
        assert_eq!(OutputFormat::try_from(4), Ok(OutputFormat::Tiff));
        assert_eq!(OutputFormat::try_from(5), Ok(OutputFormat::Bmp));
        assert_eq!(OutputFormat::try_from(6), Ok(OutputFormat::Gif));
    }

    #[test]
    fn output_format_try_from_invalid_value() {
        assert!(OutputFormat::try_from(-1).is_err());
        assert!(OutputFormat::try_from(7).is_err());
        assert!(OutputFormat::try_from(100).is_err());
    }

    #[test]
    fn output_format_extension_returns_correct_values() {
        assert_eq!(OutputFormat::Jpeg.extension(), "jpg");
        assert_eq!(OutputFormat::Png.extension(), "png");
        assert_eq!(OutputFormat::WebP.extension(), "webp");
        assert_eq!(OutputFormat::Avif.extension(), "avif");
        assert_eq!(OutputFormat::Tiff.extension(), "tiff");
        assert_eq!(OutputFormat::Bmp.extension(), "bmp");
        assert_eq!(OutputFormat::Gif.extension(), "gif");
    }

    #[test]
    fn output_format_supports_quality() {
        assert!(OutputFormat::Jpeg.supports_quality());
        assert!(OutputFormat::Avif.supports_quality());
        assert!(!OutputFormat::Png.supports_quality());
        assert!(!OutputFormat::WebP.supports_quality());
        assert!(!OutputFormat::Tiff.supports_quality());
        assert!(!OutputFormat::Bmp.supports_quality());
        assert!(!OutputFormat::Gif.supports_quality());
    }

    #[test]
    fn preserves_image_dimensions() {
        let temp = tempdir().expect("tempdir");
        let source = temp.path().join("source.png");
        create_source_image(&source);

        let formats = [
            OutputFormat::Jpeg,
            OutputFormat::Png,
            OutputFormat::WebP,
            OutputFormat::Tiff,
            OutputFormat::Bmp,
            OutputFormat::Gif,
        ];

        for format in formats {
            let output = temp.path().join(format!("output.{}", format.extension()));
            convert_image(&source, &output, format, 85).expect("conversion should succeed");

            let reopened = image::open(&output).expect("output should be readable");
            assert_eq!(reopened.width(), 200, "width should be preserved for {:?}", format);
            assert_eq!(reopened.height(), 140, "height should be preserved for {:?}", format);
        }
    }
}
