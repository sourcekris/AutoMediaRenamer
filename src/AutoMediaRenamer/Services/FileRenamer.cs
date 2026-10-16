using System.Text.Json;
using System.Text.Json.Serialization;
using AutoMediaRenamer.Models;

namespace AutoMediaRenamer.Services;

public class RenameRecord
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string BatchId { get; set; } = string.Empty;
    public string OriginalPath { get; set; } = string.Empty;
    public string NewPath { get; set; } = string.Empty;
}

[JsonSerializable(typeof(RenameRecord))]
internal partial class RenameJsonContext : JsonSerializerContext
{
}

public class RenameOptions
{
    public bool DryRun { get; set; }
    public bool Recursive { get; set; } = false;
    public bool Verbose { get; set; } = false;
}

public class RenameResult
{
    public string OriginalPath { get; set; } = string.Empty;
    public string NewPath { get; set; } = string.Empty;
    public bool Success { get; set; }
    public bool Skipped { get; set; }
    public string? Reason { get; set; }
}

public class FileRenamer
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".avi", ".m4v", ".mov", ".wmv", ".asf"
    };

    private readonly RenameOptions _options;
    private readonly string _historyFilePath;

    public FileRenamer(RenameOptions? options = null)
    {
        _options = options ?? new RenameOptions();
        string appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutoMediaRenamer");
        Directory.CreateDirectory(appDataDir);
        _historyFilePath = Path.Combine(appDataDir, "history.jsonl");
    }

    /// <summary>
    /// Processes a list of file or directory paths.
    /// </summary>
    public List<RenameResult> ProcessPaths(IEnumerable<string> paths)
    {
        var filesToProcess = new List<string>();
        var suspiciousExecutables = new List<string>();

        foreach (string path in paths)
        {
            if (Directory.Exists(path))
            {
                var searchOption = _options.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var allFiles = Directory.EnumerateFiles(path, "*.*", searchOption);
                foreach (string f in allFiles)
                {
                    if (MediaParser.IsSuspiciousExecutable(f))
                    {
                        suspiciousExecutables.Add(f);
                    }
                    else if (SupportedExtensions.Contains(Path.GetExtension(f)))
                    {
                        filesToProcess.Add(f);
                    }
                }
            }
            else if (File.Exists(path))
            {
                if (MediaParser.IsSuspiciousExecutable(path))
                {
                    suspiciousExecutables.Add(path);
                }
                else if (SupportedExtensions.Contains(Path.GetExtension(path)))
                {
                    filesToProcess.Add(path);
                }
            }
        }

        var results = new List<RenameResult>();

        // Check and prompt for any suspicious executables pretending to be media files
        foreach (string suspiciousFile in suspiciousExecutables.Distinct())
        {
            bool wasDeleted = MalwareWarningService.PromptAndDeleteIfMalware(suspiciousFile, _options.DryRun);
            results.Add(new RenameResult
            {
                OriginalPath = suspiciousFile,
                Skipped = true,
                Reason = wasDeleted
                    ? "Permanently deleted suspicious malware executable"
                    : "Suspicious malware executable (warning displayed)"
            });
        }

        string batchId = Guid.NewGuid().ToString("N");
        var recordsToLog = new List<RenameRecord>();

        foreach (string file in filesToProcess.Distinct())
        {
            var res = ProcessSingleFile(file, batchId, out var record);
            results.Add(res);
            if (record != null && !_options.DryRun)
            {
                recordsToLog.Add(record);
            }
        }

        if (recordsToLog.Count > 0)
        {
            LogHistory(recordsToLog);
        }

        return results;
    }

    private RenameResult ProcessSingleFile(string filePath, string batchId, out RenameRecord? record)
    {
        record = null;
        try
        {
            if (!File.Exists(filePath))
            {
                return new RenameResult
                {
                    OriginalPath = filePath,
                    Skipped = true,
                    Reason = "File not found"
                };
            }

            var mediaInfo = MediaParser.Parse(filePath);

            if (!mediaInfo.HasChanged)
            {
                return new RenameResult
                {
                    OriginalPath = filePath,
                    NewPath = filePath,
                    Skipped = true,
                    Reason = "Already properly named"
                };
            }

            string targetPath = mediaInfo.NewFilePath;

            // Handle collision if a different destination file already exists
            bool isCaseOnlyRename = string.Equals(
                Path.GetFullPath(filePath),
                Path.GetFullPath(targetPath),
                StringComparison.OrdinalIgnoreCase);

            if (!isCaseOnlyRename && File.Exists(targetPath))
            {
                targetPath = GetNonConflictingPath(targetPath);
            }

            if (_options.DryRun)
            {
                return new RenameResult
                {
                    OriginalPath = filePath,
                    NewPath = targetPath,
                    Success = true,
                    Reason = "Dry-run simulation"
                };
            }

            // Perform rename safely
            ExecuteRename(filePath, targetPath, isCaseOnlyRename);

            record = new RenameRecord
            {
                BatchId = batchId,
                OriginalPath = filePath,
                NewPath = targetPath
            };

            return new RenameResult
            {
                OriginalPath = filePath,
                NewPath = targetPath,
                Success = true
            };
        }
        catch (IOException ex)
        {
            return new RenameResult
            {
                OriginalPath = filePath,
                Success = false,
                Reason = $"File locked or inaccessible: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new RenameResult
            {
                OriginalPath = filePath,
                Success = false,
                Reason = $"Error: {ex.Message}"
            };
        }
    }

    private static void ExecuteRename(string source, string destination, bool isCaseOnly)
    {
        if (isCaseOnly)
        {
            // Windows NTFS requires a two-step move for case-only changes
            string tempPath = source + $".renamer_tmp_{Guid.NewGuid():N}";
            File.Move(source, tempPath);
            File.Move(tempPath, destination);
        }
        else
        {
            File.Move(source, destination);
        }
    }

    private static string GetNonConflictingPath(string targetPath)
    {
        string? dir = Path.GetDirectoryName(targetPath) ?? string.Empty;
        string nameWithoutExt = Path.GetFileNameWithoutExtension(targetPath);
        string ext = Path.GetExtension(targetPath);

        int counter = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(dir, $"{nameWithoutExt} ({counter}){ext}");
            counter++;
        } while (File.Exists(candidate));

        return candidate;
    }

    private void LogHistory(IEnumerable<RenameRecord> records)
    {
        try
        {
            using var stream = new FileStream(_historyFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream);
            foreach (var rec in records)
            {
                writer.WriteLine(JsonSerializer.Serialize(rec, RenameJsonContext.Default.RenameRecord));
            }
        }
        catch
        {
            // Logging failure should not disrupt renaming
        }
    }

    /// <summary>
    /// Reverts the most recent batch of renames.
    /// </summary>
    public List<RenameResult> UndoLastBatch()
    {
        var results = new List<RenameResult>();

        if (!File.Exists(_historyFilePath))
        {
            return results;
        }

        var lines = File.ReadAllLines(_historyFilePath);
        if (lines.Length == 0)
            return results;

        var allRecords = new List<RenameRecord>();
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var rec = JsonSerializer.Deserialize(line, RenameJsonContext.Default.RenameRecord);
                if (rec != null)
                {
                    allRecords.Add(rec);
                }
            }
            catch { }
        }

        if (allRecords.Count == 0)
            return results;

        string lastBatchId = allRecords[^1].BatchId;
        var batchRecords = allRecords.Where(r => r.BatchId == lastBatchId).ToList();

        // Undo in reverse order
        for (int i = batchRecords.Count - 1; i >= 0; i--)
        {
            var record = batchRecords[i];
            try
            {
                if (File.Exists(record.NewPath))
                {
                    bool isCaseOnly = string.Equals(record.NewPath, record.OriginalPath, StringComparison.OrdinalIgnoreCase);
                    ExecuteRename(record.NewPath, record.OriginalPath, isCaseOnly);
                    results.Add(new RenameResult
                    {
                        OriginalPath = record.NewPath,
                        NewPath = record.OriginalPath,
                        Success = true
                    });
                }
                else
                {
                    results.Add(new RenameResult
                    {
                        OriginalPath = record.NewPath,
                        NewPath = record.OriginalPath,
                        Success = false,
                        Reason = "File not found at renamed path"
                    });
                }
            }
            catch (Exception ex)
            {
                results.Add(new RenameResult
                {
                    OriginalPath = record.NewPath,
                    NewPath = record.OriginalPath,
                    Success = false,
                    Reason = ex.Message
                });
            }
        }

        // Remove the undone batch from history file
        var remainingRecords = allRecords.Where(r => r.BatchId != lastBatchId).ToList();
        using var stream = new FileStream(_historyFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream);
        foreach (var rec in remainingRecords)
        {
            writer.WriteLine(JsonSerializer.Serialize(rec, RenameJsonContext.Default.RenameRecord));
        }

        return results;
    }
}
