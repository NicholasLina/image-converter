using System;
using System.Runtime.InteropServices;
using ImageConverter.Gui.Models;

namespace ImageConverter.Gui.Services;

/// <summary>
/// Provides P/Invoke interop with the Rust native image conversion library.
/// </summary>
public static class RustInterop
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

    /// <summary>
    /// Converts an image file to the specified format.
    /// </summary>
    /// <param name="inputPath">Path to the input image file.</param>
    /// <param name="outputPath">Path where the converted image will be saved.</param>
    /// <param name="outputFormat">The target output format.</param>
    /// <param name="quality">Quality setting (1-100) for lossy formats.</param>
    /// <param name="errorMessage">Output parameter containing error details if conversion fails.</param>
    /// <returns>True if conversion succeeded, false otherwise.</returns>
    public static bool ConvertImage(
        string inputPath,
        string outputPath,
        OutputFormat outputFormat,
        int quality,
        out string errorMessage)
    {
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

    /// <summary>
    /// Estimates the output file size for a conversion without writing to disk.
    /// </summary>
    /// <param name="inputPath">Path to the input image file.</param>
    /// <param name="outputFormat">The target output format.</param>
    /// <param name="quality">Quality setting (1-100) for lossy formats.</param>
    /// <returns>The estimated size in bytes, or null if estimation failed.</returns>
    public static long? EstimateOutputSize(
        string inputPath,
        OutputFormat outputFormat,
        int quality)
    {
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
