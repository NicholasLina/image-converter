# Cloud Env Agent Setup Prompt

Use this prompt at [cursor.com/onboard](https://cursor.com/onboard):

> Configure this repository's cloud agent environment so new agents can build and test a C# Avalonia GUI with a Rust native core without manual bootstrap. Preinstall .NET SDK 8, set Rust stable via rustup, and preinstall Avalonia templates (`dotnet new install Avalonia.Templates`). Add/verify startup checks for `dotnet --version`, `rustc --version`, and `cargo --version`.

## Reference setup commands

```bash
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
rustup update stable
rustup default stable
dotnet new install Avalonia.Templates
```

## Validation commands

```bash
dotnet --version
rustc --version
cargo --version
dotnet build ImageConverter.sln
cargo test --manifest-path rust-core/Cargo.toml
```
