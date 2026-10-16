# AutoMediaRenamer

A fast, headless .NET native application for Windows that automatically normalizes and renames media files (TV shows and Movies) using intelligent token parsing and Windows Explorer context menu integration.

---

## Features

- **100% Windowless & Silent:** Built as a Windows GUI subsystem application (`WinExe`), completely eliminating console window popups or black flashes when invoked from Windows Explorer.
- **Standalone Native Binary:** Self-contained executable with sub-50ms startup and zero external runtime dependencies.
- **Context Menu Integration:** Right-click on any `.mkv`, `.mp4`, `.avi`, `.m4v`, `.mov`, `.wmv`, `.asf`, or media directory to rename files.
- **Smart Rule Set:**
  - **TV Shows:** Standardized as `<Show Title> S<Season:00>E<Episode:00>[ <Episode Title>].<ext>`
  - **Movies:** Standardized as `<Movie Title> (<Year>).<ext>`
  - **Title Case & Clean Spacing:** Strips dots, underscores, and applies standard English Title Casing.
  - **Noise Removal:** Automatically eliminates resolutions (`720p`, `1080p`, `2160p`), codecs (`x264`, `x265`, `HEVC`), sources (`WEB-DL`, `BluRay`), streaming services (`ATVP`, `NF`, `AMZN`), audio formats (`DDP5 1`, `Atmos`), scene group tags (`-MeGusta`, `-NTb`), and bracketed trackers (`[EZTVx.to]`).
- **Malware Protection:**
  - Automatically identifies suspicious `.exe` files disguised as video files (e.g., `Slow.Horses.S06E01.mkv.exe` or `Dune.2024.exe`).
  - Pops up a native Windows security warning dialog alerting you to potential malware.
  - Suggests and facilitates immediate, permanent deletion of the suspicious executable.
- **Safety First:**
  - Collision avoidance (never overwrites existing files).
  - Handles Windows case-only renames safely via two-stage moves.
  - `--dry-run` simulation mode.
  - `--undo` to instantly revert the most recent batch of renames.

---

## Example Transformations

| Original Filename | Renamed Result |
|---|---|
| `Slow.Horses.S06E01.720p.HEVC.x265-MeGusta[EZTVx.to].mkv` | `Slow Horses S06E01.mkv` |
| `Slow Horses S06E03 Resurrection 720p ATVP WEB-DL DDP5 1 H 264-NTb[EZTVx.to].mkv` | `Slow Horses S06E03 Resurrection.mkv` |
| `Severance.S02E01.Hello.Ms.Cobel.1080p.WEB-DL.DDP5.1.Atmos.H.264-FLUX.mkv` | `Severance S02E01 Hello Ms Cobel.mkv` |
| `Dune.Part.Two.2024.2160p.UHD.BluRay.x265-FLUX.mkv` | `Dune Part Two (2024).mkv` |

---

## Setup & Explorer Integration

Register the context menu by running the executable with `--register`:
```powershell
.\AutoMediaRenamer.exe --register
```
This automatically registers the **"Auto Rename Media"** option in your Windows right-click context menu (under `HKCU`, requiring no administrator permissions).

To remove the context menu integration at any time:
```powershell
.\AutoMediaRenamer.exe --unregister
```

---

## CLI Usage

```powershell
# Rename specific files:
AutoMediaRenamer.exe "Slow.Horses.S06E01.720p.mkv"

# Rename all media files in a directory:
AutoMediaRenamer.exe "D:\Downloads"

# Preview renames without modifying files:
AutoMediaRenamer.exe --dry-run "D:\Downloads"

# Revert the last rename operation:
AutoMediaRenamer.exe --undo

# Unregister from Windows Explorer context menu:
AutoMediaRenamer.exe --unregister
```

---

## Building and Testing

```powershell
# Run the test suite (24 unit tests):
dotnet test

# Publish self-contained single-file executable:
dotnet publish src/AutoMediaRenamer/AutoMediaRenamer.csproj -c Release -r win-x64
```
