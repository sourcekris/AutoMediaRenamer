# Automatic Media Renamer - Implementation Plan

An automated, headless .NET native application for Windows that normalizes and renames downloaded media files (TV shows and Movies) using intelligent token parsing and Windows Explorer context menu integration.

---

## 1. Project Overview & Requirements

### Key Goals
- **Headless / Silent Execution:** Built with `<OutputType>WinExe</OutputType>` so Windows Explorer launches it completely windowless (zero console popup/flash), with `AttachConsole` support when run from a terminal.
- **Native .NET Performance:** Built targeting **.NET 10** with **Native AOT** (`PublishAot=true`) compilation for instant startup (sub-50ms) when invoked from Windows Explorer.
- **Explorer Context Menu Integration:** Right-click directly on `.mkv` files (or folders containing media) to trigger automatic renaming.
- **Intelligent Media Parsing:**
  - **TV Shows:** Standardized format: `<Show Title> S<Season:00>E<Episode:00>[ <Episode Title>].<ext>`
  - **Movies:** Standardized format: `<Movie Title> (<Year>).<ext>`
  - **Clean Title Formatting:** Title-casing, replace dots/underscores with spaces, strip redundant metadata (resolutions, codecs, release groups, audio specs, URLs, and tracker brackets).

---

## 2. Parsing Engine Architecture

Raw scene/P2P filenames contain structured but noisy tokens:
```
[Show / Movie Title] [Season & Episode or Year] [Optional Title] [Quality/Source] [Codec] [Audio] [Group] [Bracketed Tags]
Example:
Slow Horses S06E03 Resurrection 720p ATVP WEB-DL DDP5 1 H 264-NTb[EZTVx.to].mkv
└── Show Title ──┘ └─ S/E ─┘ └── Ep Title ──┘ └────────── Strip All Metadata Junk ──────────────┘ └── Ext ──┘
```

### Parsing Pipeline Strategy
1. **Filename Sanitization:**
   - Separate extension from filename without extension.
   - Strip leading/trailing brackets, URLs (`[EZTVx.to]`, `[TGx]`, `www.*.com`).
2. **Media Type & Anchor Detection:**
   - **TV Anchor:** Regex search for Season/Episode patterns:
     - `(?i)[._ -]S(?<season>\d{1,2})E(?<episode>\d{1,3})(?:[E-]E?(?<episode2>\d{1,3}))?[._ -]?`
     - Alternative season/episode formats: `(?i)[._ -](?<season>\d{1,2})x(?<episode>\d{2})[._ -]`
   - **Movie Anchor:** Regex search for 4-digit Year between 1900 and 2099 bounded by delimiters.
3. **Episode Title vs. Noise Filtering:**
   - Any tokens appearing *after* the Season/Episode tag are split.
   - Detect the "junk boundary" using a dictionary of known tags:
     - **Resolutions:** `480p`, `576p`, `720p`, `1080p`, `1080i`, `2160p`, `4K`, `UHD`.
     - **Sources:** `WEB-DL`, `WEBRip`, `BluRay`, `BDRip`, `BRRip`, `HDTV`, `DVDRip`, `REMUX`, `ATVP`, `NF`, `AMZN`, `DSNP`, `HMAX`.
     - **Codecs:** `x264`, `x265`, `h264`, `h265`, `HEVC`, `AVC`, `AV1`, `XviD`, `10bit`.
     - **Audio:** `DDP5\.?1`, `DD5\.?1`, `AAC.*`, `AC3`, `DTS.*`, `TrueHD`, `Atmos`.
     - **Release Groups / Scene flags:** `-MeGusta`, `-NTb`, `-FLUX`, `PROPER`, `REPACK`, `MULTI`, `RERIP`.
   - Text between the Season/Episode tag and the first noise tag is retained as the **Episode Title**.
4. **Title Normalization:**
   - Convert dots (`.`), underscores (`_`), and redundant hyphens to single spaces.
   - Apply English Title-Case rules (e.g. `slow horses` -> `Slow Horses`, preserving common roman numerals like `II`, `III`).
   - Trim leading/trailing whitespace.
5. **Collision & Safety Check:**
   - Verify that destination path != source path.
   - Avoid accidental overwrite if destination file already exists (append `(1)`, report conflict, or log).

---

## 3. Windows Explorer Shell Extension Integration

To integrate without requiring complex COM C++ registration:
1. **Windows Registry Integration (Classic & Modern Windows):**
   - Register shell command verbs under:
     - `HKEY_CLASSES_ROOT\SystemFileAssociations\.mkv\shell\AutoMediaRenamer`
     - `HKEY_CLASSES_ROOT\SystemFileAssociations\.mp4\shell\AutoMediaRenamer`
     - `HKEY_CLASSES_ROOT\Directory\shell\AutoMediaRenamer` (for right-clicking folders)
   - Menu Text: `Auto Rename Media`
   - Command: `"C:\Path\To\AutoMediaRenamer.exe" "%1"`
2. **Self-Registration CLI Flags:**
   - `AutoMediaRenamer.exe --register`: Automatically configures the Windows Registry for the current user (`HKCU\Software\Classes`).
   - `AutoMediaRenamer.exe --unregister`: Cleans up the registry entries cleanly.

---

## 4. CLI & Operational Modes

| Command / Flag | Purpose |
|---|---|
| `AutoMediaRenamer.exe <file1> <file2> ...` | Renames the provided files silently. |
| `AutoMediaRenamer.exe <directory>` | Recursively or top-level scans and renames supported media files. |
| `--dry-run` | Simulates renames and outputs changes without touching disk. |
| `--undo` | Reverts the most recent rename operation using a local session audit log (`renames.log`). |
| `--register` / `--unregister` | Adds or removes Explorer context menu integration. |

---

## 5. Phased Implementation Roadmap

### Phase 1: Project Setup & Core Parsing Engine
- Initialize .NET 10 solution and console project (`AutoMediaRenamer`).
- Enable Native AOT compilation settings in `.csproj`.
- Implement `MediaParser`:
  - Tokenization & Regex rules for TV shows (`S00E00`) and Movies (`Year`).
  - Known noise list & boundary detection.
  - Title cleaner and Title-Casing engine.
- Write unit tests covering diverse real-world scene release formats:
  - `Slow.Horses.S06E01.720p.HEVC.x265-MeGusta[EZTVx.to].mkv`
  - `Slow Horses S06E03 Resurrection 720p ATVP WEB-DL DDP5 1 H 264-NTb[EZTVx.to].mkv`
  - Multi-part episodes: `Show.Title.S01E01-E02.mkv`
  - Movies: `Dune.Part.Two.2024.2160p.UHD.BluRay.x265-FLUX.mkv`

### Phase 2: File System Engine, Safety & Logging
- Implement `FileRenamer`:
  - Handle single files and directory scans (`.mkv`, `.mp4`, `.avi`, `.m4v`, `.mov`, `.wmv`, `.asf`).
  - Malware Protection: Detect fake media files with `.exe` extension (e.g. `.mkv.exe`), pop up native Windows warning dialog, and suggest permanent deletion.
  - File locking / in-use checks (skip files currently open by torrent clients or players).
  - Conflict resolution (never overwrite existing files).
  - Transaction log (`rename-history.jsonl`) for tracking operations and enabling `--undo`.
- Implement CLI argument parsing (`System.CommandLine` or lightweight zero-dependency parser for instant AOT startup).

### Phase 3: Windows Shell Extension Registration
- Implement registry installer (`--register`, `--unregister`).
- Target `HKCU\Software\Classes\SystemFileAssociations\.mkv\shell\AutoMediaRenamer` (non-admin install support).
- Optional Windows toast notification on completion (so user knows files were renamed in headless mode).

### Phase 4: AOT Build, Benchmarking & End-to-End Testing
- Compile with `dotnet publish -c Release -r win-x64 --self-contained /p:PublishAot=true`.
- Verify binary size, memory usage, and sub-second startup execution.
- Validate Windows Explorer right-click execution on sample test files.

---

## 6. Example Transformations

| Original Filename | Expected Result |
|---|---|
| `Slow.Horses.S06E01.720p.HEVC.x265-MeGusta[EZTVx.to].mkv` | `Slow Horses S06E01.mkv` |
| `Slow Horses S06E03 Resurrection 720p ATVP WEB-DL DDP5 1 H 264-NTb[EZTVx.to].mkv` | `Slow Horses S06E03 Resurrection.mkv` |
| `Severance.S02E01.Hello.Ms.Cobel.1080p.WEB-DL.DDP5.1.Atmos.H.264-FLUX.mkv` | `Severance S02E01 Hello Ms Cobel.mkv` |
| `The.Bear.S03E01.Tomorrow.1080p.Hulu.WEB-DL.DDP5.1.H.264-NTb.mkv` | `The Bear S03E01 Tomorrow.mkv` |
| `Oppenheimer.2023.IMAX.2160p.UHD.BluRay.x265-TERMiNAL.mkv` | `Oppenheimer (2023).mkv` |
