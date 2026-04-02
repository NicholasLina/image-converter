using ImageConverter.Gui.Services;
using Xunit;

namespace ImageConverter.Tests.Services;

/// <summary>
/// Tests for FileSystemService.
/// </summary>
public class FileSystemServiceTests
{
    [Fact]
    public void GetSupportedExtensions_ReturnsNonEmptyList()
    {
        var extensions = FileSystemService.GetSupportedExtensions();
        Assert.NotNull(extensions);
        Assert.NotEmpty(extensions);
    }

    [Fact]
    public void GetSupportedExtensions_ContainsCommonFormats()
    {
        var extensions = FileSystemService.GetSupportedExtensions();
        Assert.Contains(".png", extensions);
        Assert.Contains(".jpg", extensions);
        Assert.Contains(".jpeg", extensions);
        Assert.Contains(".gif", extensions);
        Assert.Contains(".bmp", extensions);
        Assert.Contains(".webp", extensions);
    }

    [Theory]
    [InlineData("image.png", true)]
    [InlineData("image.jpg", true)]
    [InlineData("image.jpeg", true)]
    [InlineData("image.gif", true)]
    [InlineData("image.bmp", true)]
    [InlineData("image.webp", true)]
    [InlineData("image.avif", true)]
    [InlineData("image.tiff", true)]
    [InlineData("image.tif", true)]
    public void IsSupportedInput_CommonFormats_ReturnsTrue(string fileName, bool expected)
    {
        bool result = FileSystemService.IsSupportedInput(fileName);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("image.txt", false)]
    [InlineData("image.pdf", false)]
    [InlineData("image.doc", false)]
    [InlineData("image.mp4", false)]
    [InlineData("noextension", false)]
    public void IsSupportedInput_UnsupportedFormats_ReturnsFalse(string fileName, bool expected)
    {
        bool result = FileSystemService.IsSupportedInput(fileName);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("IMAGE.PNG")]
    [InlineData("Image.Jpg")]
    [InlineData("photo.JPEG")]
    public void IsSupportedInput_CaseInsensitive(string fileName)
    {
        bool result = FileSystemService.IsSupportedInput(fileName);
        Assert.True(result);
    }

    [Fact]
    public void BuildOutputPath_NoCollision_ReturnsBasePath()
    {
        string tempDir = Path.GetTempPath();
        string uniqueName = $"test_{Guid.NewGuid()}";
        
        string result = FileSystemService.BuildOutputPath(tempDir, uniqueName, "jpg");
        
        Assert.EndsWith($"{uniqueName}.jpg", result);
        Assert.StartsWith(tempDir, result);
    }

    [Fact]
    public void BuildOutputPath_WithCollision_AppendsNumber()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            string baseName = "test";
            string extension = "jpg";
            
            string firstPath = Path.Combine(tempDir, $"{baseName}.{extension}");
            File.WriteAllText(firstPath, "test");
            
            string result = FileSystemService.BuildOutputPath(tempDir, baseName, extension);
            
            Assert.EndsWith($"{baseName}_1.{extension}", result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void BuildOutputPath_MultipleCollisions_IncrementsNumber()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            string baseName = "test";
            string extension = "jpg";
            
            File.WriteAllText(Path.Combine(tempDir, $"{baseName}.{extension}"), "test");
            File.WriteAllText(Path.Combine(tempDir, $"{baseName}_1.{extension}"), "test");
            File.WriteAllText(Path.Combine(tempDir, $"{baseName}_2.{extension}"), "test");
            
            string result = FileSystemService.BuildOutputPath(tempDir, baseName, extension);
            
            Assert.EndsWith($"{baseName}_3.{extension}", result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void EnumerateSupportedFiles_NonExistentDirectory_ReturnsEmptyList()
    {
        string nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        
        var result = FileSystemService.EnumerateSupportedFiles(nonExistentPath);
        
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void EnumerateSupportedFiles_WithSupportedFiles_ReturnsOnlySupported()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "image1.png"), "test");
            File.WriteAllText(Path.Combine(tempDir, "image2.jpg"), "test");
            File.WriteAllText(Path.Combine(tempDir, "document.txt"), "test");
            File.WriteAllText(Path.Combine(tempDir, "video.mp4"), "test");
            
            var result = FileSystemService.EnumerateSupportedFiles(tempDir);
            
            Assert.Equal(2, result.Count);
            Assert.Contains(result, path => path.EndsWith("image1.png"));
            Assert.Contains(result, path => path.EndsWith("image2.jpg"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void EnumerateSupportedFiles_Recursive_FindsFilesInSubdirectories()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        string subDir = Path.Combine(tempDir, "subdir");
        Directory.CreateDirectory(subDir);
        
        try
        {
            File.WriteAllText(Path.Combine(tempDir, "root.png"), "test");
            File.WriteAllText(Path.Combine(subDir, "nested.jpg"), "test");
            
            var result = FileSystemService.EnumerateSupportedFiles(tempDir);
            
            Assert.Equal(2, result.Count);
            Assert.Contains(result, path => path.EndsWith("root.png"));
            Assert.Contains(result, path => path.EndsWith("nested.jpg"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
