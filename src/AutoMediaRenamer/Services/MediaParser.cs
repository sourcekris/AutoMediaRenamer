using System.IO;
using System.Text.RegularExpressions;
using AutoMediaRenamer.Models;

namespace AutoMediaRenamer.Services;

public partial class MediaParser
{
    // Regex for standard TV show format: S01E02, S1E2, S01E01-E02, S01E01E02
    private static readonly Regex TvPatternStandard = new(
        @"(?:[._ -]|^)[sS](?<season>\d{1,2})[eE](?<episode>\d{1,3})(?:(?:[-_ ]?[eE]|[-])(?<episode2>\d{1,3}))?(?:[._ -]|$)",
        RegexOptions.Compiled);

    // Regex for alternate TV show format: 1x02, 01x02
    private static readonly Regex TvPatternAlt = new(
        @"(?:[._ -]|^)(?<season>\d{1,2})x(?<episode>\d{2,3})(?:[._ -]|$)",
        RegexOptions.Compiled);

    // Regex for Movie Year: 4 digits (1900-2099) bounded by non-alphanumeric or parenthesis
    private static readonly Regex MovieYearPattern = new(
        @"(?:[._ -]|\()(?<year>(?:19|20)\d{2})(?:\)|[._ -]|$)",
        RegexOptions.Compiled);

    // Regex for leading release group / tracker tags: e.g. [EZTV], [TGx], (Rarbg)
    private static readonly Regex LeadingTagPattern = new(
        @"^(\[[^\]]*\]|\([^\)]*\))\s*",
        RegexOptions.Compiled);

    // Regex for bracketed URLs or tags anywhere: [EZTVx.to], [eztv], (1080p), etc.
    private static readonly Regex BracketedTagPattern = new(
        @"\[.*?\]|\(.*?\)",
        RegexOptions.Compiled);

    // Regex for website domains / URLs: e.g. www.Torrenting.com, EZTVx.to
    private static readonly Regex DomainPattern = new(
        @"\b(?:www\.)?[a-zA-Z0-9-]+\.(?:com|to|org|net|co|cc|me|info|tv)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Known noise tokens marking the boundary where episode titles stop and technical metadata begins
    private static readonly Regex NoiseBoundaryPattern = new(
        @"(?i)(?:\b|_|\.)(?:480p|480i|576p|576i|720p|720i|1080p|1080i|2160p|4k|uhd|hd|" +
        @"web[-_.]?dl|web[-_.]?rip|webdl|webrip|web|bluray|blu-ray|bdrip|brrip|hdtv|pdtv|dvdrip|dvd|remux|telesync|camrip|" +
        @"x264|x265|h[-_. ]?264|h[-_. ]?265|hevc|avc|av1|xvid|divx|10bit|8bit|hdr(?:10(?:\+)?)?|dv|dovi|sdr|" +
        @"atvp|apple[-_.]?tv\+?|netflix|nf|amzn|amazon|dsnp|disney\+?|hmax|hbomax|max|hulu|peacock|pcok|paramount\+?|iplayer|sho|showtime|starz|cwt|syfy|bbc|itv|" +
        @"ddp?[\s._-]?5[\s._-]?1|dd[\s._-]?2[\s._-]?0|ddp?[\s._-]?2[\s._-]?0|aac(?:\d\.\d)?|ac3|ac-3|dts(?:-hd)?(?:[\s._-]ma)?|truehd|atmos|flac|mp3|eac3|2ch|6ch|8ch|" +
        @"proper|repack|rerip|real[\s._-]proper|unrated|extended|director'?s[\s._-]cut|remastered|multi|subbed|dubbed|internal|complete|" +
        @"-[a-zA-Z0-9]+$|" +
        @"\[.*?\])(?:\b|_|\.|$)",
        RegexOptions.Compiled);

    /// <summary>
    /// Parses a file path or filename into structured MediaInfo with normalized name.
    /// </summary>
    public static MediaInfo Parse(string filePathOrName)
    {
        string originalFileName = Path.GetFileName(filePathOrName);
        string extension = Path.GetExtension(originalFileName);
        string nameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);

        var mediaInfo = new MediaInfo
        {
            OriginalFilePath = filePathOrName,
            OriginalFileName = originalFileName,
            Extension = extension
        };

        // Remove leading bracket tags like [EZTV] or [TGx]
        string sanitizedName = LeadingTagPattern.Replace(nameWithoutExt, string.Empty).Trim();

        // 1. Try parsing as TV Episode (Standard SxxExx first, then xxXxx)
        var tvMatch = TvPatternStandard.Match(sanitizedName);
        if (!tvMatch.Success)
        {
            tvMatch = TvPatternAlt.Match(sanitizedName);
        }

        if (tvMatch.Success && int.TryParse(tvMatch.Groups["season"].Value, out int season) &&
            int.TryParse(tvMatch.Groups["episode"].Value, out int episode))
        {
            mediaInfo.Type = MediaType.TvEpisode;
            mediaInfo.Season = season;
            mediaInfo.Episode = episode;

            if (tvMatch.Groups["episode2"].Success && int.TryParse(tvMatch.Groups["episode2"].Value, out int ep2))
            {
                mediaInfo.Episode2 = ep2;
            }

            // Extract Show Title (everything before the TV match)
            string rawShowTitle = Regex.Replace(sanitizedName[..tvMatch.Index], @"[\s._:-]+$", string.Empty);
            mediaInfo.Title = TitleCaseHelper.ToTitleCase(rawShowTitle);

            // Extract Episode Title candidate (everything after the TV match)
            string afterTvMatch = sanitizedName[(tvMatch.Index + tvMatch.Length)..];
            mediaInfo.EpisodeTitle = ExtractEpisodeTitle(afterTvMatch);

            // Construct new filename
            string seasonEp = mediaInfo.Episode2.HasValue
                ? $"S{season:D2}E{episode:D2}-E{mediaInfo.Episode2.Value:D2}"
                : $"S{season:D2}E{episode:D2}";

            string formattedName = string.IsNullOrEmpty(mediaInfo.EpisodeTitle)
                ? $"{mediaInfo.Title} {seasonEp}{extension}"
                : $"{mediaInfo.Title} {seasonEp} {mediaInfo.EpisodeTitle}{extension}";

            mediaInfo.NewFileName = CleanFinalFileName(formattedName);
            mediaInfo.NewFilePath = UpdateFilePath(mediaInfo.OriginalFilePath, mediaInfo.NewFileName);
            return mediaInfo;
        }

        // 2. Try parsing as Movie (look for 4-digit Year)
        var movieMatch = MovieYearPattern.Match(sanitizedName);
        if (movieMatch.Success && int.TryParse(movieMatch.Groups["year"].Value, out int year))
        {
            mediaInfo.Type = MediaType.Movie;
            mediaInfo.Year = year;

            string rawMovieTitle = sanitizedName[..movieMatch.Index];
            mediaInfo.Title = TitleCaseHelper.ToTitleCase(rawMovieTitle);

            string formattedName = $"{mediaInfo.Title} ({year}){extension}";
            mediaInfo.NewFileName = CleanFinalFileName(formattedName);
            mediaInfo.NewFilePath = UpdateFilePath(mediaInfo.OriginalFilePath, mediaInfo.NewFileName);
            return mediaInfo;
        }

        // 3. Fallback: Clean up raw filename (strip noise, bracketed urls, apply title case)
        mediaInfo.Type = MediaType.Unknown;
        string cleanedFallback = CleanGenericTitle(sanitizedName);
        mediaInfo.Title = cleanedFallback;
        mediaInfo.NewFileName = CleanFinalFileName($"{cleanedFallback}{extension}");
        mediaInfo.NewFilePath = UpdateFilePath(mediaInfo.OriginalFilePath, mediaInfo.NewFileName);
        return mediaInfo;
    }

    private static string? ExtractEpisodeTitle(string afterTvMatch)
    {
        if (string.IsNullOrWhiteSpace(afterTvMatch))
            return null;

        // Strip leading delimiters (e.g. " - ", " : ", " . ", " _ ") between season/episode and episode title
        string working = Regex.Replace(afterTvMatch, @"^[\s._:-]+", string.Empty);
        if (string.IsNullOrWhiteSpace(working))
            return null;

        // Remove trailing bracketed urls or release group tags first
        working = DomainPattern.Replace(working, string.Empty);
        working = BracketedTagPattern.Replace(working, string.Empty);

        // Find the earliest index where technical noise begins
        var match = NoiseBoundaryPattern.Match(working);
        string candidate = match.Success ? working[..match.Index] : working;

        // Only check for a trailing release group trailer (e.g. -NTb) if there is title text before it
        int dashIdx = candidate.LastIndexOf('-');
        if (dashIdx > 0 && dashIdx < candidate.Length - 1)
        {
            string beforeDash = candidate[..dashIdx].Trim();
            string afterDash = candidate[(dashIdx + 1)..].Trim();
            // If beforeDash has letters and afterDash is a short alphanumeric group name (<= 12 chars without spaces/dots)
            if (beforeDash.Any(char.IsLetter) && afterDash.Length > 0 && afterDash.Length <= 12 && !afterDash.Contains(' ') && !afterDash.Contains('.'))
            {
                candidate = beforeDash;
            }
        }

        string cleanTitle = TitleCaseHelper.ToTitleCase(candidate).Trim('-', ' ', '.', '_', ':');

        // Verify it contains actual letters and isn't just numbers or garbage
        if (cleanTitle.Length > 0 && cleanTitle.Any(char.IsLetter))
        {
            return cleanTitle;
        }

        return null;
    }

    private static string CleanGenericTitle(string raw)
    {
        string withoutDomain = DomainPattern.Replace(raw, string.Empty);
        string withoutBrackets = BracketedTagPattern.Replace(withoutDomain, string.Empty);
        var match = NoiseBoundaryPattern.Match(withoutBrackets);
        string candidate = match.Success ? withoutBrackets[..match.Index] : withoutBrackets;
        return TitleCaseHelper.ToTitleCase(candidate);
    }

    private static string CleanFinalFileName(string fileName)
    {
        // Collapse multiple spaces
        string result = Regex.Replace(fileName, @"\s+", " ").Trim();
        // Ensure invalid path characters are not present
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            result = result.Replace(c.ToString(), string.Empty);
        }
        return result;
    }

    private static string UpdateFilePath(string originalPath, string newFileName)
    {
        if (string.IsNullOrEmpty(originalPath))
            return newFileName;

        string? dir = Path.GetDirectoryName(originalPath);
        return string.IsNullOrEmpty(dir) ? newFileName : Path.Combine(dir, newFileName);
    }

    /// <summary>
    /// Checks if a file path or name has characteristics of a media file (TV show/movie pattern or embedded video extension).
    /// </summary>
    public static bool IsMediaLikeFileName(string fileName)
    {
        string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        string sanitized = LeadingTagPattern.Replace(nameWithoutExt, string.Empty).Trim();

        // Check for double extension like .mkv.exe, .mp4.exe
        if (sanitized.EndsWith(".mkv", StringComparison.OrdinalIgnoreCase) ||
            sanitized.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
            sanitized.EndsWith(".avi", StringComparison.OrdinalIgnoreCase) ||
            sanitized.EndsWith(".m4v", StringComparison.OrdinalIgnoreCase) ||
            sanitized.EndsWith(".mov", StringComparison.OrdinalIgnoreCase) ||
            sanitized.EndsWith(".wmv", StringComparison.OrdinalIgnoreCase) ||
            sanitized.EndsWith(".asf", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Check for TV episode pattern
        if (TvPatternStandard.IsMatch(sanitized) || TvPatternAlt.IsMatch(sanitized))
        {
            return true;
        }

        // Check for Movie Year pattern
        if (MovieYearPattern.IsMatch(sanitized))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if a file is an executable (.exe) masquerading as a media file.
    /// </summary>
    public static bool IsSuspiciousExecutable(string filePathOrName)
    {
        string ext = Path.GetExtension(filePathOrName);
        if (!string.Equals(ext, ".exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return IsMediaLikeFileName(filePathOrName);
    }
}
