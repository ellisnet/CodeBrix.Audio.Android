using System;
using System.Globalization;
using CodeBrix.Audio.Android.Internal;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Audio.Android.Tests;

// AndroidDeviceInfos is compiled into this project as a linked source file (see the csproj): it
// builds every DeviceInfo AndroidAudioEngine puts in PlaybackDevices and CaptureDevices, from the
// AudioDeviceInfo id and display name the engine reads on the device.
public sealed class AndroidDeviceInfosTests
{
    [Fact]
    public void OsDeviceIdBackend_is_android()
        => AndroidDeviceInfos.OsDeviceIdBackend.Should().Be("android");

    [Fact]
    public void SystemDefault_is_id_zero_and_names_no_os_device()
    {
        //Arrange

        //Act
        var info = AndroidDeviceInfos.SystemDefault();

        //Assert
        info.Id.Should().Be((nint)0);
        info.Name.Should().Be("System default");
        info.IsDefault.Should().BeTrue();
        info.SupportedDataFormats.Should().BeEmpty();
        info.OsDeviceId.Should().BeNull();
        info.OsDeviceIdBackend.Should().BeNull();
    }

    [Theory]
    [InlineData(1, "1")]
    [InlineData(27, "27")]
    [InlineData(4096, "4096")]
    [InlineData(int.MaxValue, "2147483647")]
    public void ForDevice_reports_the_android_device_id_as_the_os_device_id(int androidId, string expected)
    {
        //Arrange

        //Act
        var info = AndroidDeviceInfos.ForDevice(androidId, "USB-Audio - C922 Pro Stream Webcam");

        //Assert
        info.OsDeviceId.Should().Be(expected);
        info.OsDeviceIdBackend.Should().Be("android");
    }

    [Fact]
    public void ForDevice_keeps_the_engine_id_name_and_empty_formats()
    {
        //Arrange

        //Act
        var info = AndroidDeviceInfos.ForDevice(27, "Galaxy Tab S8");

        //Assert
        info.Id.Should().Be((nint)27);
        info.Name.Should().Be("Galaxy Tab S8");
        info.IsDefault.Should().BeFalse();
        info.SupportedDataFormats.Should().BeEmpty();
    }

    [Fact]
    public void ForDevice_formats_the_id_with_the_invariant_culture()
    {
        //Arrange - a culture whose negative sign is not "-"
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NegativeSign = "~";
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = culture;
        string osDeviceId;

        //Act
        try { osDeviceId = AndroidDeviceInfos.ForDevice(-5, "odd").OsDeviceId; }
        finally { CultureInfo.CurrentCulture = previous; }

        //Assert
        osDeviceId.Should().Be("-5");
    }

    [Fact]
    public void ForDevice_ids_are_stable_across_enumerations()
    {
        //Arrange
        var first = AndroidDeviceInfos.ForDevice(12, "Built-in microphone");

        //Act
        var second = AndroidDeviceInfos.ForDevice(12, "Built-in microphone");

        //Assert
        second.Id.Should().Be(first.Id);
        second.OsDeviceId.Should().Be(first.OsDeviceId);
        second.OsDeviceIdBackend.Should().Be(first.OsDeviceIdBackend);
    }
}
