using AutoMediaRenamer.Services;
using Xunit;

namespace AutoMediaRenamer.Tests;

public class FileRenamerTests : IDisposable
{
    private readonly string _tempDirectory;

    public FileRenamerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "AutoMediaRenamerTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try { Directory.Delete(_tempDirectory, true); } catch { }
        }
    }

    [Fact]
    public void ProcessPaths_RenamesTvShowFileOnDisk()
    {
        string original = Path.Combine(_tempDirectory, "Slow.Horses.S06E01.720p.HEVC.x265-MeGusta.mkv");
        File.WriteAllText(original, "test content");

        var renamer = new FileRenamer();
        var results = renamer.ProcessPaths(new[] { original });

        string expected = Path.Combine(_tempDirectory, "Slow Horses S06E01.mkv");
        Assert.Single(results);
        Assert.True(results[0].Success);
        Assert.False(File.Exists(original));
        Assert.True(File.Exists(expected));
    }

    [Fact]
    public void ProcessPaths_WithDryRun_DoesNotModifyDisk()
    {
        string original = Path.Combine(_tempDirectory, "Slow.Horses.S06E01.720p.HEVC.x265-MeGusta.mkv");
        File.WriteAllText(original, "test content");

        var renamer = new FileRenamer(new RenameOptions { DryRun = true });
        var results = renamer.ProcessPaths(new[] { original });

        string expected = Path.Combine(_tempDirectory, "Slow Horses S06E01.mkv");
        Assert.Single(results);
        Assert.True(results[0].Success);
        Assert.True(File.Exists(original));
        Assert.False(File.Exists(expected));
    }

    [Fact]
    public void ProcessPaths_CollisionAvoidance_AppendsNumber()
    {
        string existing = Path.Combine(_tempDirectory, "Slow Horses S06E01.mkv");
        File.WriteAllText(existing, "existing file");

        string fileToRename = Path.Combine(_tempDirectory, "Slow.Horses.S06E01.1080p.mkv");
        File.WriteAllText(fileToRename, "new file");

        var renamer = new FileRenamer();
        var results = renamer.ProcessPaths(new[] { fileToRename });

        string expected = Path.Combine(_tempDirectory, "Slow Horses S06E01 (1).mkv");
        Assert.Single(results);
        Assert.True(results[0].Success);
        Assert.True(File.Exists(existing));
        Assert.True(File.Exists(expected));
    }

    [Fact]
    public void UndoLastBatch_RevertsRenamedFile()
    {
        string original = Path.Combine(_tempDirectory, "Slow.Horses.S06E01.720p.mkv");
        File.WriteAllText(original, "test content");

        var renamer = new FileRenamer();
        renamer.ProcessPaths(new[] { original });

        string renamed = Path.Combine(_tempDirectory, "Slow Horses S06E01.mkv");
        Assert.True(File.Exists(renamed));

        var undoResults = renamer.UndoLastBatch();
        Assert.Single(undoResults);
        Assert.True(undoResults[0].Success);
        Assert.True(File.Exists(original));
        Assert.False(File.Exists(renamed));
    }

    [Fact]
    public void ProcessPaths_WhenMediaExecutableFoundAndConfirmed_PermanentlyDeletesFile()
    {
        string fakeMediaExe = Path.Combine(_tempDirectory, "Slow.Horses.S06E01.720p.mkv.exe");
        File.WriteAllText(fakeMediaExe, "fake executable malware content");

        try
        {
            // Simulate user clicking "Yes" to delete
            MalwareWarningService.TestPromptHandler = (path, msg, caption) => true;

            var renamer = new FileRenamer();
            var results = renamer.ProcessPaths(new[] { fakeMediaExe });

            Assert.Single(results);
            Assert.True(results[0].Skipped);
            Assert.Contains("Permanently deleted", results[0].Reason);
            Assert.False(File.Exists(fakeMediaExe));
        }
        finally
        {
            MalwareWarningService.TestPromptHandler = null;
        }
    }

    [Fact]
    public void ProcessPaths_WhenMediaExecutableFoundAndCancelled_RetainsFileAndDoesNotRename()
    {
        string fakeMediaExe = Path.Combine(_tempDirectory, "Slow.Horses.S06E01.720p.mkv.exe");
        File.WriteAllText(fakeMediaExe, "fake executable malware content");

        try
        {
            // Simulate user clicking "No"
            MalwareWarningService.TestPromptHandler = (path, msg, caption) => false;

            var renamer = new FileRenamer();
            var results = renamer.ProcessPaths(new[] { fakeMediaExe });

            Assert.Single(results);
            Assert.True(results[0].Skipped);
            Assert.Contains("warning displayed", results[0].Reason);
            Assert.True(File.Exists(fakeMediaExe));
        }
        finally
        {
            MalwareWarningService.TestPromptHandler = null;
        }
    }

    [Fact]
    public void ProcessPaths_DirectoryContainingMediaAndFakeExe_RenamesMediaAndDeletesMalware()
    {
        string validMedia = Path.Combine(_tempDirectory, "Slow.Horses.S06E01.720p.mkv");
        string fakeMediaExe = Path.Combine(_tempDirectory, "Slow.Horses.S06E02.720p.mkv.exe");
        File.WriteAllText(validMedia, "valid video");
        File.WriteAllText(fakeMediaExe, "fake malware");

        try
        {
            MalwareWarningService.TestPromptHandler = (path, msg, caption) => true;

            var renamer = new FileRenamer();
            var results = renamer.ProcessPaths(new[] { _tempDirectory });

            Assert.Equal(2, results.Count);
            // Valid media is renamed
            Assert.True(File.Exists(Path.Combine(_tempDirectory, "Slow Horses S06E01.mkv")));
            // Fake exe is deleted
            Assert.False(File.Exists(fakeMediaExe));
        }
        finally
        {
            MalwareWarningService.TestPromptHandler = null;
        }
    }

    [Fact]
    public void ProcessPaths_RenamesAsfFileOnDisk()
    {
        string original = Path.Combine(_tempDirectory, "Slow.Horses.S06E01.720p.HEVC.asf");
        File.WriteAllText(original, "test asf content");

        var renamer = new FileRenamer();
        var results = renamer.ProcessPaths(new[] { original });

        string expected = Path.Combine(_tempDirectory, "Slow Horses S06E01.asf");
        Assert.Single(results);
        Assert.True(results[0].Success);
        Assert.False(File.Exists(original));
        Assert.True(File.Exists(expected));
    }
}
