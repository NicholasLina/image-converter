using System;
using System.Runtime.InteropServices;
using ImageConverter.Gui.Models;

namespace ImageConverter.Gui.Services;

/// <summary>
/// Provides P/Invoke interop with the Rust native image conversion library.
/// Implements <see cref="IImageConverter"/> so it can be replaced with a mock in tests.
/// </summary>
public sealed class RustInterop : IImageConverter
{
    private const string LibraryName = "image_core";

    [DllImport(LibraryName, EntryPoint = "convert_image_ffi", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool ConvertImageNative(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string inputPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string outputPath,
        int outputFormat,
        byte quality,
        out IntPtr errorPtr);

    [DllImport(LibraryName, EntryPoint = "estimate_output_size_ffi", CallingConvention = CallingConvention.Cdecl)]
    private static extern long EstimateOutputSizeNative(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string inputPath,
        int outputFormat,
        byte quality);

    [DllImport(LibraryName, EntryPoint = "free_rust_string", CallingConvention = CallingConvention.Cdecl)]
    private static extern void FreeRustString(IntPtr value);

    /// <inheritdoc />
    public bool ConvertImage(
        string inputPath,
        string outputPath,
        OutputFormat outputFormat,
        int quality,
        out string errorMessage)
    {
        ArgumentNullException.ThrowIfNull(inputPath);
        ArgumentNullException.ThrowIfNull(outputPath);

        IntPtr errorPtr = IntPtr.Zero;
        errorMessage = string.Empty;

        try
        {
            bool success = ConvertImageNative(
                inputPath,
                outputPath,
                (int)outputFormat,
                (byte)Math.Clamp(quality, 1, 100),
                out errorPtr);

            if (success)
            {
                return true;
            }

            errorMessage = errorPtr == IntPtr.Zero
                ? "Image conversion failed."
                : Marshal.PtrToStringUTF8(errorPtr) ?? "Image conversion failed.";
            return false;
        }
        catch (DllNotFoundException ex)
        {
            errorMessage = $"Rust core library not found: {ex.Message}";
            return false;
        }
        catch (EntryPointNotFoundException ex)
        {
            errorMessage = $"Rust API mismatch: {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = $"Unexpected interop error: {ex.Message}";
            return false;
        }
        finally
        {
            if (errorPtr != IntPtr.Zero)
            {
                FreeRustString(errorPtr);
            }
        }
    }

    /// <inheritdoc />
    public long? EstimateOutputSize(
        string inputPath,
        OutputFormat outputFormat,
        int quality)
    {
        ArgumentNullException.ThrowIfNull(inputPath);

        try
        {
            long value = EstimateOutputSizeNative(
                inputPath,
                (int)outputFormat,
                (byte)Math.Clamp(quality, 1, 100));
            return value < 0 ? null : value;
        }
        catch
        {
            return null;
        }
    }
}
