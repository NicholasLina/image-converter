# Test Suite Overview

This document provides an overview of the comprehensive test suite added to the image-converter project.

## Test Statistics

- **Total Tests**: 111
  - C# Tests: 91 (71 unit + 20 integration)
  - Rust Tests: 20
- **Pass Rate**: 100%

## Running Tests

### C# Tests

Run all C# tests:
```bash
dotnet test tests/ImageConverter.Tests/ImageConverter.Tests.csproj
```

Run only unit tests (faster):
```bash
dotnet test tests/ImageConverter.Tests/ImageConverter.Tests.csproj --filter "FullyQualifiedName!~Integration"
```

Run only integration tests:
```bash
dotnet test tests/ImageConverter.Tests/ImageConverter.Tests.csproj --filter "FullyQualifiedName~Integration"
```

### Rust Tests

Run all Rust tests:
```bash
cargo test --manifest-path rust-core/Cargo.toml
```

Run with verbose output:
```bash
cargo test --manifest-path rust-core/Cargo.toml -- --nocapture
```

## Test Coverage

### C# Unit Tests (71 tests)

#### Models Tests
- **OutputFormatTests** (14 tests)
  - Format quality support validation
  - File extension correctness
  - Enum value validation

- **ByteFormatTests** (13 tests)
  - Byte size formatting (B, KB, MB, GB, TB)
  - Decimal precision
  - Edge cases (0 bytes, large values)

- **ConversionJobTests** (12 tests)
  - Property initialization
  - Property change notifications
  - Status management
  - File name extraction

#### Services Tests
- **FileSystemServiceTests** (32 tests)
  - File extension validation
  - Directory enumeration
  - Path collision handling
  - Recursive file discovery
  - Case-insensitive matching

### C# Integration Tests (20 tests)

#### ConversionIntegrationTests
- **Format Conversion** (6 tests)
  - All formats: JPEG, PNG, WebP, AVIF, TIFF, BMP, GIF
  - Quality parameter handling

- **Batch Operations** (5 tests)
  - Multiple file conversion
  - Progress callbacks
  - Mixed success/failure scenarios

- **Size Estimation** (4 tests)
  - Single file estimation
  - Batch estimation
  - Quality impact on size

- **Error Handling** (3 tests)
  - Non-existent input files
  - Invalid paths
  - Null handling

- **File System** (2 tests)
  - Output directory creation
  - Path collision handling

### Rust Tests (20 tests)

#### Core Functionality
- **Format Conversion** (7 tests)
  - JPEG, PNG, WebP, AVIF, TIFF, BMP, GIF conversion
  - Subdirectory creation

- **Quality Handling** (3 tests)
  - Quality parameter clamping
  - JPEG quality impact
  - AVIF quality impact

- **Size Estimation** (3 tests)
  - Lossless format estimation
  - Estimate vs actual size matching
  - Quality impact on size

- **Error Handling** (2 tests)
  - Non-existent input handling
  - Failed estimation handling

- **Format Utilities** (5 tests)
  - OutputFormat enum conversion
  - File extension mapping
  - Quality support detection
  - Dimension preservation

## Code Organization Improvements

### New Service Classes

#### FileSystemService
Handles all file system operations:
- `GetSupportedExtensions()`: Returns list of supported file extensions
- `IsSupportedInput(path)`: Validates if a file is a supported image format
- `EnumerateSupportedFiles(folder)`: Recursively finds all supported images
- `BuildOutputPath(folder, name, ext)`: Creates collision-safe output paths

#### ConversionService
Manages image conversion operations:
- `ConvertImageAsync()`: Converts a single image
- `EstimateOutputSizeAsync()`: Estimates output size for a single image
- `EstimateBatchAsync()`: Estimates sizes for multiple images
- `ConvertBatchAsync()`: Converts multiple images with progress tracking

### Refactored MainWindow
- Removed business logic (moved to services)
- Cleaner, more focused UI code
- Better separation of concerns
- Easier to test and maintain

## Documentation

All public classes, methods, and properties now have XML documentation comments:
- IntelliSense support in IDEs
- API documentation generation
- Clear parameter descriptions
- Return value documentation

## Test Dependencies

### C# Test Project
- **xUnit**: Testing framework
- **SixLabors.ImageSharp**: Cross-platform image creation for tests
- **Microsoft.NET.Test.Sdk**: Test execution

### Rust Test Dependencies
- **tempfile**: Temporary directory management
- **image**: Image manipulation (already in main dependencies)

## Continuous Integration

The test suite is designed to run in CI/CD pipelines:
- Fast execution (< 2 seconds for all tests)
- No external dependencies required
- Cross-platform compatible
- Clear pass/fail reporting

## Future Enhancements

Potential areas for additional testing:
1. UI automation tests using Avalonia test framework
2. Performance benchmarks for large images
3. Memory usage profiling tests
4. Concurrent conversion stress tests
5. Format-specific edge case tests (transparency, color profiles, etc.)
