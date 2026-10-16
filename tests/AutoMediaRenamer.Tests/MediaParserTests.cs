using AutoMediaRenamer.Models;
using AutoMediaRenamer.Services;
using Xunit;

namespace AutoMediaRenamer.Tests;

public class MediaParserTests
{
    [Theory]
    [InlineData(
        "Slow.Horses.S06E01.720p.HEVC.x265-MeGusta[EZTVx.to].mkv",
        "Slow Horses S06E01.mkv",
        "Slow Horses", 6, 1, null, null)]
    [InlineData(
        "Slow Horses S06E03 Resurrection 720p ATVP WEB-DL DDP5 1 H 264-NTb[EZTVx.to].mkv",
        "Slow Horses S06E03 Resurrection.mkv",
        "Slow Horses", 6, 3, null, "Resurrection")]
    [InlineData(
        "Severance.S02E01.Hello.Ms.Cobel.1080p.WEB-DL.DDP5.1.Atmos.H.264-FLUX.mkv",
        "Severance S02E01 Hello Ms Cobel.mkv",
        "Severance", 2, 1, null, "Hello Ms Cobel")]
    [InlineData(
        "The.Bear.S03E01.Tomorrow.1080p.Hulu.WEB-DL.DDP5.1.H.264-NTb.mkv",
        "The Bear S03E01 Tomorrow.mkv",
        "The Bear", 3, 1, null, "Tomorrow")]
    [InlineData(
        "Breaking.Bad.1x02.Cat's.in.the.Bag.720p.BluRay.mkv",
        "Breaking Bad S01E02 Cat's in the Bag.mkv",
        "Breaking Bad", 1, 2, null, "Cat's in the Bag")]
    [InlineData(
        "The.Office.US.S04E01E02.Fun.Run.1080p.mkv",
        "The Office US S04E01-E02 Fun Run.mkv",
        "The Office US", 4, 1, 2, "Fun Run")]
    [InlineData(
        "Doctor.Who.2005.S01E01.Rose.720p.mkv",
        "Doctor Who 2005 S01E01 Rose.mkv",
        "Doctor Who 2005", 1, 1, null, "Rose")]
    [InlineData(
        "American Restoration - 2x02 - Ridin Route 66 HDTV XviD-CRiMSON.avi",
        "American Restoration S02E02 Ridin Route 66.avi",
        "American Restoration", 2, 2, null, "Ridin Route 66")]
    [InlineData(
        "American Restoration - 2x02 - Ridin Route 66.avi",
        "American Restoration S02E02 Ridin Route 66.avi",
        "American Restoration", 2, 2, null, "Ridin Route 66")]
    [InlineData(
        "American Restoration - 2x02 - Pilot.avi",
        "American Restoration S02E02 Pilot.avi",
        "American Restoration", 2, 2, null, "Pilot")]
    [InlineData(
        "American.Restoration.-.2x02.-.Ridin.Route.66.HDTV.XviD-CRiMSON.avi",
        "American Restoration S02E02 Ridin Route 66.avi",
        "American Restoration", 2, 2, null, "Ridin Route 66")]
    public void Parse_TvShows_ReturnsExpected(
        string rawFileName,
        string expectedNewFileName,
        string expectedTitle,
        int expectedSeason,
        int expectedEpisode,
        int? expectedEpisode2,
        string? expectedEpisodeTitle)
    {
        var result = MediaParser.Parse(rawFileName);

        Assert.Equal(MediaType.TvEpisode, result.Type);
        Assert.Equal(expectedTitle, result.Title);
        Assert.Equal(expectedSeason, result.Season);
        Assert.Equal(expectedEpisode, result.Episode);
        Assert.Equal(expectedEpisode2, result.Episode2);
        Assert.Equal(expectedEpisodeTitle, result.EpisodeTitle);
        Assert.Equal(expectedNewFileName, result.NewFileName);
    }

    [Theory]
    [InlineData(
        "Dune.Part.Two.2024.2160p.UHD.BluRay.x265-FLUX.mkv",
        "Dune Part Two (2024).mkv",
        "Dune Part Two", 2024)]
    [InlineData(
        "Oppenheimer.2023.IMAX.2160p.UHD.BluRay.x265-TERMiNAL.mkv",
        "Oppenheimer (2023).mkv",
        "Oppenheimer", 2023)]
    [InlineData(
        "Gladiator.II.2024.1080p.WEB-DL.DDP5.1.Atmos.H.264-FLUX.mkv",
        "Gladiator II (2024).mkv",
        "Gladiator II", 2024)]
    public void Parse_Movies_ReturnsExpected(
        string rawFileName,
        string expectedNewFileName,
        string expectedTitle,
        int expectedYear)
    {
        var result = MediaParser.Parse(rawFileName);

        Assert.Equal(MediaType.Movie, result.Type);
        Assert.Equal(expectedTitle, result.Title);
        Assert.Equal(expectedYear, result.Year);
        Assert.Equal(expectedNewFileName, result.NewFileName);
    }

    [Fact]
    public void Parse_FullPath_PreservesDirectoryInNewFilePath()
    {
        string fullPath = @"C:\Media\Downloads\Slow.Horses.S06E01.720p.HEVC.x265-MeGusta.mkv";
        var result = MediaParser.Parse(fullPath);

        Assert.Equal("Slow Horses S06E01.mkv", result.NewFileName);
        Assert.Equal(@"C:\Media\Downloads\Slow Horses S06E01.mkv", result.NewFilePath);
        Assert.True(result.HasChanged);
    }

    [Fact]
    public void Parse_AlreadyFormatted_ReportsNoChange()
    {
        string file = "Slow Horses S06E01.mkv";
        var result = MediaParser.Parse(file);

        Assert.Equal("Slow Horses S06E01.mkv", result.NewFileName);
        Assert.False(result.HasChanged);
    }

    [Fact]
    public void Parse_WithLeadingBracketTag_StripsTagCleanly()
    {
        string file = "[TGx] Slow Horses S06E03 Resurrection 1080p WEB-DL.mkv";
        var result = MediaParser.Parse(file);

        Assert.Equal("Slow Horses S06E03 Resurrection.mkv", result.NewFileName);
    }

    [Theory]
    [InlineData("Slow.Horses.S06E01.720p.mkv.exe", true)]
    [InlineData("Slow.Horses.S06E01.asf.exe", true)]
    [InlineData("Slow.Horses.S06E01.exe", true)]
    [InlineData("Slow Horses S06E03 Resurrection.exe", true)]
    [InlineData("Dune.Part.Two.2024.exe", true)]
    [InlineData("Dune.2024.1080p.mkv.exe", true)]
    [InlineData("Dune.2024.1080p.asf.exe", true)]
    [InlineData("Slow.Horses.S06E01.720p.mkv", false)]
    [InlineData("Slow.Horses.S06E01.720p.asf", false)]
    [InlineData("setup.exe", false)]
    [InlineData("chrome.exe", false)]
    [InlineData("notepad.exe", false)]
    public void IsSuspiciousExecutable_DetectsMediaExeAccurately(string fileName, bool expected)
    {
        bool actual = MediaParser.IsSuspiciousExecutable(fileName);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Parse_AsfTvShow_RenamesProperly()
    {
        var result = MediaParser.Parse("Slow.Horses.S06E01.720p.HEVC.asf");
        Assert.Equal("Slow Horses S06E01.asf", result.NewFileName);
    }

    [Fact]
    public void Parse_AsfMovie_RenamesProperly()
    {
        var result = MediaParser.Parse("Gladiator.2000.720p.asf");
        Assert.Equal("Gladiator (2000).asf", result.NewFileName);
    }
}
