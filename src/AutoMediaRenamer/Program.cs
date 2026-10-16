using System.Runtime.InteropServices;
using AutoMediaRenamer.Services;

namespace AutoMediaRenamer;

public static class Program
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    private const int ATTACH_PARENT_PROCESS = -1;
    private const uint GENERIC_WRITE = 0x40000000;
    private const uint FILE_SHARE_WRITE = 2;
    private const uint OPEN_EXISTING = 3;

    public static int Main(string[] args)
    {
        // If launched from a console/terminal, attach to parent console so output is visible
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (!Console.IsOutputRedirected && AttachConsole(ATTACH_PARENT_PROCESS))
            {
                var hConOut = CreateFile("CONOUT$", GENERIC_WRITE, FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                if (hConOut != IntPtr.Zero && hConOut != new IntPtr(-1))
                {
                    var safeHandle = new Microsoft.Win32.SafeHandles.SafeFileHandle(hConOut, ownsHandle: true);
                    var writer = new StreamWriter(new FileStream(safeHandle, FileAccess.Write)) { AutoFlush = true };
                    Console.SetOut(writer);
                    Console.SetError(writer);
                }
            }
        }

        if (args.Length == 0)
        {
            PrintUsage();
            return 0;
        }

        bool dryRun = false;
        bool recursive = false;
        bool verbose = false;
        bool register = false;
        bool unregister = false;
        bool undo = false;
        var paths = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            switch (arg.ToLowerInvariant())
            {
                case "--help":
                case "-h":
                case "/?":
                    PrintUsage();
                    return 0;

                case "--register":
                case "-r":
                    register = true;
                    break;

                case "--unregister":
                case "-u":
                    unregister = true;
                    break;

                case "--undo":
                    undo = true;
                    break;

                case "--dry-run":
                case "-d":
                    dryRun = true;
                    verbose = true; // Always verbose in dry run
                    break;

                case "--recursive":
                case "-s":
                    recursive = true;
                    break;

                case "--verbose":
                case "-v":
                    verbose = true;
                    break;

                default:
                    if (!arg.StartsWith('-'))
                    {
                        paths.Add(arg);
                    }
                    break;
            }
        }

        if (register)
        {
            bool ok = ShellRegistrar.Register();
            return ok ? 0 : 1;
        }

        if (unregister)
        {
            bool ok = ShellRegistrar.Unregister();
            return ok ? 0 : 1;
        }

        var renamer = new FileRenamer(new RenameOptions
        {
            DryRun = dryRun,
            Recursive = recursive,
            Verbose = verbose
        });

        if (undo)
        {
            Console.WriteLine("Reverting last rename operation...");
            var undoResults = renamer.UndoLastBatch();
            if (undoResults.Count == 0)
            {
                Console.WriteLine("No recent rename batch found to undo.");
                return 0;
            }

            foreach (var res in undoResults)
            {
                if (res.Success)
                {
                    Console.WriteLine($"Reverted: {Path.GetFileName(res.OriginalPath)} -> {Path.GetFileName(res.NewPath)}");
                }
                else
                {
                    Console.WriteLine($"Failed to revert: {Path.GetFileName(res.OriginalPath)} ({res.Reason})");
                }
            }
            return 0;
        }

        if (paths.Count == 0)
        {
            Console.Error.WriteLine("Error: No files or directories specified.");
            PrintUsage();
            return 1;
        }

        var results = renamer.ProcessPaths(paths);

        int renamedCount = 0;
        int skippedCount = 0;
        int failedCount = 0;

        foreach (var res in results)
        {
            if (res.Success)
            {
                renamedCount++;
                if (verbose || dryRun)
                {
                    string tag = dryRun ? "[DRY-RUN]" : "[RENAMED]";
                    Console.WriteLine($"{tag} {Path.GetFileName(res.OriginalPath)} -> {Path.GetFileName(res.NewPath)}");
                }
            }
            else if (res.Skipped)
            {
                skippedCount++;
                if (verbose)
                {
                    Console.WriteLine($"[SKIPPED] {Path.GetFileName(res.OriginalPath)} ({res.Reason})");
                }
            }
            else
            {
                failedCount++;
                Console.Error.WriteLine($"[FAILED] {Path.GetFileName(res.OriginalPath)}: {res.Reason}");
            }
        }

        if (verbose || dryRun)
        {
            Console.WriteLine($"\nSummary: {renamedCount} renamed, {skippedCount} skipped, {failedCount} failed.");
        }

        return failedCount > 0 ? 1 : 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine(@"
AutoMediaRenamer - Automatic TV Show & Movie File Renamer
=========================================================

Usage:
  AutoMediaRenamer.exe [options] <file1> <file2> ...
  AutoMediaRenamer.exe [options] <directory>

Options:
  --dry-run, -d      Simulate renames and print changes without touching files.
  --recursive, -s    Recursively process subdirectories when a directory is specified.
  --undo             Revert the most recent rename batch.
  --verbose, -v      Output detailed progress information.
  --register, -r     Register 'Auto Rename Media' in Windows Explorer context menu.
  --unregister, -u   Remove 'Auto Rename Media' from Windows Explorer context menu.
  --help, -h         Show this help screen.

Examples:
  AutoMediaRenamer.exe ""Slow.Horses.S06E01.720p.mkv""
  AutoMediaRenamer.exe --dry-run ""D:\Downloads""
  AutoMediaRenamer.exe --register
");
    }
}
