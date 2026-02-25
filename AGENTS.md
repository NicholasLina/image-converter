# AGENTS.md

## Cursor Cloud specific instructions

### Overview

This is a cross-platform desktop **Image Converter** app: a C# Avalonia GUI (`gui/ImageConverter.Gui/`) calling a Rust native core (`rust-core/`) via P/Invoke FFI. There are no services, databases, or Docker dependencies.

### System prerequisites

- **.NET SDK 8.0+** — `sudo apt-get install -y dotnet-sdk-8.0`
- **Rust stable** — `rustup update stable && rustup default stable`
- **Avalonia Templates** (optional) — `dotnet new install Avalonia.Templates`

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
