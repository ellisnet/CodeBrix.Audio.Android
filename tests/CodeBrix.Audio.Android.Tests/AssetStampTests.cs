using System;
using System.IO;
using CodeBrix.Audio.Android.Internal;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Audio.Android.Tests;

// AssetStamp is compiled into this project as a linked source file (see the csproj): it is the
// platform-independent half of AndroidPackagedAssets - path validation, on-disk layout and the
// per-install stamp - and the part that can be pinned down without an APK.
public sealed class AssetStampTests
{
    [Theory]
    [InlineData("FluidR3_GM.sf2", "FluidR3_GM.sf2")]
    [InlineData("instruments/FluidR3_GM.sf2", "instruments/FluidR3_GM.sf2")]
    [InlineData("instruments\\piano\\", "instruments/piano")]
    [InlineData("/leading/slash.sfz", "leading/slash.sfz")]
    [InlineData("  padded.wav  ", "padded.wav")]
    public void NormalizeAssetPath_accepts_relative_paths_and_normalizes_them(string input, string expected)
        => AssetStamp.NormalizeAssetPath(input).Should().Be(expected);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("/")]
    [InlineData("../escape.sf2")]
    [InlineData("instruments/../../escape.sf2")]
    [InlineData("./here.sf2")]
    [InlineData("a//b.sf2")]
    [InlineData("C:/absolute.sf2")]
    public void NormalizeAssetPath_rejects_paths_that_are_empty_absolute_or_escape_the_assets(string input)
    {
        //Arrange
        Action normalize = () => AssetStamp.NormalizeAssetPath(input);

        //Act / Assert
        normalize.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void DestinationFor_mirrors_the_asset_path_under_the_root()
    {
        //Arrange
        var root = Path.Combine("files", "codebrix-audio-assets");

        //Act
        var destination = AssetStamp.DestinationFor(root, "instruments/FluidR3_GM.sf2");

        //Assert
        destination.Should().Be(Path.Combine(root, "instruments", "FluidR3_GM.sf2"));
        AssetStamp.StampPathFor(destination).Should().Be(destination + ".codebrix-stamp");
        AssetStamp.PartialPathFor(destination).Should().Be(destination + ".codebrix-partial");
    }

    [Fact]
    public void Compose_produces_a_stamp_that_matches_itself_and_nothing_else()
    {
        //Arrange
        var stamp = AssetStamp.Compose(42, 1700000000000, "FluidR3_GM.sf2");

        //Act / Assert
        stamp.Should().Be("1|42|1700000000000|FluidR3_GM.sf2");
        AssetStamp.Matches(stamp, stamp).Should().BeTrue();
        AssetStamp.Matches(stamp + Environment.NewLine, stamp).Should().BeTrue();
        AssetStamp.Matches(AssetStamp.Compose(43, 1700000000000, "FluidR3_GM.sf2"), stamp).Should().BeFalse();
        AssetStamp.Matches(AssetStamp.Compose(42, 1700000000001, "FluidR3_GM.sf2"), stamp).Should().BeFalse();
        AssetStamp.Matches(AssetStamp.Compose(42, 1700000000000, "other.sf2"), stamp).Should().BeFalse();
        AssetStamp.Matches(null, stamp).Should().BeFalse();
    }
}
