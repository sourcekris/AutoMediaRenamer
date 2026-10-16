using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AutoMediaRenamer.Services;

public static class ShellRegistrar
{
    private static readonly string[] Extensions = { ".mkv", ".mp4", ".avi", ".m4v", ".mov", ".wmv", ".asf" };
    private const string MenuKeyName = "AutoMediaRenamer";
    private const string MenuVerbText = "Auto Rename Media";
    private const string AppliesToCondition =
        "System.FileExtension:=.mkv OR System.FileExtension:=.mp4 OR System.FileExtension:=.avi OR " +
        "System.FileExtension:=.m4v OR System.FileExtension:=.mov OR System.FileExtension:=.wmv OR " +
        "System.FileExtension:=.asf OR System.FileExtension:=.exe";

    /// <summary>
    /// Registers the context menu in Windows Explorer for current user (HKCU, no admin needed).
    /// Configured with MultiSelectModel="Document" and AppliesTo to support multi-file selection.
    /// </summary>
    public static bool Register(string? exePath = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("Context menu registration is only supported on Windows.");
            return false;
        }

        exePath ??= Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "AutoMediaRenamer.exe");
        exePath = Path.GetFullPath(exePath);

        string commandString = $"\"{exePath}\" \"%1\"";
        string dirCommandString = $"\"{exePath}\" \"%V\"";

        try
        {
            // 1. Register for Any File (*) with AppliesTo and MultiSelectModel for multi-selection support
            {
                string starKeyPath = $@"Software\Classes\*\shell\{MenuKeyName}";
                using var starKey = Registry.CurrentUser.CreateSubKey(starKeyPath);
                starKey.SetValue("", MenuVerbText);
                starKey.SetValue("Icon", exePath);
                starKey.SetValue("MultiSelectModel", "Document");
                starKey.SetValue("AppliesTo", AppliesToCondition);

                using var cmdKey = starKey.CreateSubKey("command");
                cmdKey.SetValue("", commandString);
            }

            // 2. Register for each specific file extension under SystemFileAssociations with MultiSelectModel
            foreach (var ext in Extensions)
            {
                string keyPath = $@"Software\Classes\SystemFileAssociations\{ext}\shell\{MenuKeyName}";
                using var key = Registry.CurrentUser.CreateSubKey(keyPath);
                key.SetValue("", MenuVerbText);
                key.SetValue("Icon", exePath);
                key.SetValue("MultiSelectModel", "Document");

                using var cmdKey = key.CreateSubKey("command");
                cmdKey.SetValue("", commandString);
            }

            // 3. Register for Folders (Directory)
            {
                string dirKeyPath = $@"Software\Classes\Directory\shell\{MenuKeyName}";
                using var key = Registry.CurrentUser.CreateSubKey(dirKeyPath);
                key.SetValue("", MenuVerbText);
                key.SetValue("Icon", exePath);
                key.SetValue("MultiSelectModel", "Document");

                using var cmdKey = key.CreateSubKey("command");
                cmdKey.SetValue("", commandString);
            }

            // 4. Register for Directory Background (Right clicking blank space inside a folder)
            {
                string bgKeyPath = $@"Software\Classes\Directory\Background\shell\{MenuKeyName}";
                using var key = Registry.CurrentUser.CreateSubKey(bgKeyPath);
                key.SetValue("", MenuVerbText);
                key.SetValue("Icon", exePath);

                using var cmdKey = key.CreateSubKey("command");
                cmdKey.SetValue("", dirCommandString);
            }

            Console.WriteLine($"Successfully registered '{MenuVerbText}' in Windows Explorer context menu with multi-select support!");
            Console.WriteLine($"Linked to executable: {exePath}");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to register context menu: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Unregisters the context menu from Windows Explorer.
    /// </summary>
    public static bool Unregister()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("Context menu unregistration is only supported on Windows.");
            return false;
        }

        try
        {
            try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\*\shell\{MenuKeyName}", throwOnMissingSubKey: false); } catch { }

            foreach (var ext in Extensions)
            {
                string keyPath = $@"Software\Classes\SystemFileAssociations\{ext}\shell\{MenuKeyName}";
                try { Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false); } catch { }
            }

            try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\Directory\shell\{MenuKeyName}", throwOnMissingSubKey: false); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\Directory\Background\shell\{MenuKeyName}", throwOnMissingSubKey: false); } catch { }

            Console.WriteLine("Successfully unregistered AutoMediaRenamer from Windows Explorer context menu.");
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to unregister context menu: {ex.Message}");
            return false;
        }
    }
}
