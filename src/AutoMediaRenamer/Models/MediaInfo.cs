namespace AutoMediaRenamer.Models;

public enum MediaType
{
    Unknown,
    TvEpisode,
    Movie
}

public class MediaInfo
{
    public string OriginalFilePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public MediaType Type { get; set; } = MediaType.Unknown;

    public string Title { get; set; } = string.Empty;
    public int? Season { get; set; }
    public int? Episode { get; set; }
    public int? Episode2 { get; set; }
    public string? EpisodeTitle { get; set; }
    public int? Year { get; set; }

    public string NewFileName { get; set; } = string.Empty;
    public string NewFilePath { get; set; } = string.Empty;

    public bool HasChanged => !string.Equals(OriginalFileName, NewFileName, StringComparison.Ordinal);
}
