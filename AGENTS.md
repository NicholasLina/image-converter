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
### Overview

This is a cross-platform desktop **Image Converter** app: a C# Avalonia GUI (`gui/ImageConverter.Gui/`) calling a Rust native core (`rust-core/`) via P/Invoke FFI. There are no services, databases, or Docker dependencies.

### System prerequisites (handled by the VM update script)

The update script preinstalls these on every cloud agent startup — no manual bootstrap needed:

- **.NET SDK 8.0+** (`dotnet-sdk-8.0`)
- **Rust stable** (set as default via `rustup default stable`)
- **Avalonia Templates** (`dotnet new install Avalonia.Templates`)
- **NuGet restore** (`dotnet restore ImageConverter.sln`)

### Build / Run / Test

Standard commands are in the `README.md`. Quick reference:

| Action | Command |
|--------|---------|
| Build all | `dotnet build ImageConverter.sln` |
| Run GUI | `dotnet run --project gui/ImageConverter.Gui/ImageConverter.Gui.csproj` |
| Rust tests | `cargo test --manifest-path rust-core/Cargo.toml` |

The `.csproj` MSBuild target `BuildRustCore` auto-runs `cargo build --release` before the .NET build, so `dotnet build` alone builds both layers.

### Gotchas

- **Avalonia init-order null guard**: `QualitySlider_OnValueChanged` fires during XAML initialization before `QualityLabel` is hydrated. A null guard (`if (QualityLabel is null) return;`) is needed at line 222 of `MainWindow.axaml.cs` to prevent a `NullReferenceException` at startup.
- **DISPLAY variable**: The Avalonia GUI requires `DISPLAY=:1` (already set in cloud VMs). If the app exits immediately without error output, verify `echo $DISPLAY`.
- **Two windows on first launch**: The first `dotnet run` may spawn two overlapping windows (one from a stale background process). This is harmless; interact with the foreground (right) window.
- **No lint tool configured**: The codebase has no dedicated linter (no `.editorconfig` enforcement, no `dotnet format` config, no `clippy` CI). Build warnings from `dotnet build` and `cargo build` serve as the closest equivalent.
