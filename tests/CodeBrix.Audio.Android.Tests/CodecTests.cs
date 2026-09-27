using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using CodeBrix.Audio.Engine.Backends.MiniAudio;
using CodeBrix.Audio.Engine.Structs;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Audio.Android.Tests;

// Exercises the codec half of the native library through a HOST build of the same C
// sources (artifacts/native/host/libcodebrix_miniaudio.so, built by tools/test-host.sh),
// via the shared engine's codec factory. Nothing here touches Android's device stack.
public sealed class CodecTests
{
    [Theory]
    [InlineData("tone.wav", "wav")]
    [InlineData("tone.mp3", "mp3")]
    [InlineData("tone.flac", "flac")]
    [InlineData("tone.ogg", "ogg")]
    public void codec_only_binary_decodes_resamples_and_seeks_without_device_io(string file, string format)
    {
        //Arrange
        using var input = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Assets", file));
        var factory = new MiniAudioCodecFactory();
        using var decoder = factory.CreateDecoder(input, format, AudioFormat.DvdHq);
        var first = new float[2048];
        var repeated = new float[2048];

        //Act
        int decodedFirst = decoder.Decode(first);
        bool seekToStart = decoder.Seek(0);
        int decodedRepeated = decoder.Decode(repeated);
        bool seekMidway = decoder.Seek((decoder.Length / 4) * 2);
        int decodedAfterSeek = decoder.Decode(new float[2048]);

        //Assert
        decodedFirst.Should().Be(first.Length);
        seekToStart.Should().BeTrue();
        decodedRepeated.Should().Be(repeated.Length);
        decoder.SampleRate.Should().Be(48000);
        decoder.Channels.Should().Be(2);
        decoder.Length.Should().BeGreaterThan(10000);
        first.Should().Contain(s => Math.Abs(s) > 0.001f);
        for (int i = 0; i < first.Length; ++i) Math.Abs(first[i] - repeated[i]).Should().BeInRange(0f, 0.0001f);
        seekMidway.Should().BeTrue();
        decodedAfterSeek.Should().BeGreaterThan(0);
    }

    [Fact]
    public void host_library_contains_vorbis_and_no_miniaudio_devices()
    {
        //Arrange
        var library = NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, "libcodebrix_miniaudio.so"));
        try
        {
            //Act
            bool hasVorbis = NativeLibrary.TryGetExport(library, "sf_has_vorbis", out _);
            bool hasDeviceInit = NativeLibrary.TryGetExport(library, "ma_device_init", out _);
            bool hasContextInit = NativeLibrary.TryGetExport(library, "ma_context_init", out _);

            //Assert
            hasVorbis.Should().BeTrue();
            hasDeviceInit.Should().BeFalse();
            hasContextInit.Should().BeFalse();
        }
        finally { NativeLibrary.Free(library); }
    }

    [Fact]
    public void codec_only_encoder_writes_readable_wav()
    {
        //Arrange
        var factory = new MiniAudioCodecFactory();
        using var bytes = new MemoryStream();
        var signal = Enumerable.Range(0, 960).Select(i => (float)(0.1 * Math.Sin(i * 0.07))).ToArray();

        //Act
        using (var encoder = factory.CreateEncoder(bytes, "wav", AudioFormat.DvdHq)) encoder.Encode(signal);
        bytes.Position = 0;
        using var decoder = factory.CreateDecoder(bytes, "wav", AudioFormat.DvdHq);
        var decoded = new float[signal.Length];
        int decodedCount = decoder.Decode(decoded);

        //Assert
        decodedCount.Should().Be(signal.Length);
        decoded.Should().Equal(signal);
    }
}
