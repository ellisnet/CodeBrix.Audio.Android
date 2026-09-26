using System.Runtime.InteropServices;
using CodeBrix.Audio.Engine.Backends.MiniAudio;
using CodeBrix.Audio.Engine.Structs;
using Xunit;

namespace CodeBrix.Audio.Android.HostTests;

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
        using var decoder = factory.CreateDecoder(input, format, AudioFormat.DvdHq)!;
        var first = new float[2048]; var repeated = new float[2048];

        //Act
        Assert.Equal(first.Length, decoder.Decode(first));
        Assert.True(decoder.Seek(0));
        Assert.Equal(repeated.Length, decoder.Decode(repeated));

        //Assert
        Assert.Equal(48000, decoder.SampleRate);
        Assert.Equal(2, decoder.Channels);
        Assert.True(decoder.Length > 10000);
        Assert.Contains(first, s => Math.Abs(s) > 0.001f);
        for (int i = 0; i < first.Length; ++i) Assert.InRange(Math.Abs(first[i] - repeated[i]), 0, 0.0001f);
        Assert.True(decoder.Seek((decoder.Length / 4) * 2));
        Assert.True(decoder.Decode(repeated) > 0);
    }

    [Fact]
    public void host_library_contains_vorbis_and_no_miniaudio_devices()
    {
        //Arrange
        var library = NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, "libcodebrix_miniaudio.so"));
        try
        {
            //Act / Assert
            Assert.True(NativeLibrary.TryGetExport(library, "sf_has_vorbis", out _));
            Assert.False(NativeLibrary.TryGetExport(library, "ma_device_init", out _));
            Assert.False(NativeLibrary.TryGetExport(library, "ma_context_init", out _));
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
        using (var encoder = factory.CreateEncoder(bytes, "wav", AudioFormat.DvdHq)!) encoder.Encode(signal);
        bytes.Position = 0;
        using var decoder = factory.CreateDecoder(bytes, "wav", AudioFormat.DvdHq)!;
        var decoded = new float[signal.Length];

        //Assert
        Assert.Equal(signal.Length, decoder.Decode(decoded));
        Assert.Equal(signal, decoded);
    }
}
