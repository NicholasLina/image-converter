# AGENTS.md

## Cursor Cloud specific instructions

### Prerequisites (handled by VM update script)

- .NET SDK 8.0 (`dotnet-sdk-8.0` via apt)
- Rust stable toolchain (`rustup update stable && rustup default stable`)

### Build & Run

Standard commands are documented in `README.md`. Key points:

- `dotnet build ImageConverter.sln` builds both the C# GUI and Rust core (MSBuild auto-invokes `cargo build --release`).
- `dotnet run --project gui/ImageConverter.Gui/ImageConverter.Gui.csproj` runs the desktop app.
- `cargo test --manifest-path rust-core/Cargo.toml` runs Rust unit tests.

### Non-obvious caveats

- **DISPLAY must be set.** The Avalonia GUI requires `DISPLAY=:1` (already set in cloud VMs via TigerVNC). The app will silently exit without it.
- **NullReferenceException on startup.** `QualitySlider_OnValueChanged` fires during XAML init before `QualityLabel` is hydrated. A null guard (`if (QualityLabel is null) return;`) is required at line 222 of `MainWindow.axaml.cs`.
- **No linter configured.** There is no dedicated lint tool; `dotnet build` warnings serve as the code quality check.
- **First build is slow.** The initial `cargo build --release` compiles ~90 Rust crates (including rav1e for AVIF). Subsequent builds are incremental.
- **Rust toolchain version matters.** Some transitive dependencies (e.g., `aligned` crate) require edition 2024 support, so Rust 1.85+ is needed. The VM update script ensures this.
