using System;
using System.Collections.Generic;
using System.Diagnostics;
using CodeBrix.Audio.Android.Internal;
using CodeBrix.Audio.Engine.Abstracts;
using CodeBrix.Audio.Engine.Abstracts.Devices;
using CodeBrix.Audio.Engine.Enums;
using CodeBrix.Audio.Engine.Structs;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Audio.Android.Tests;

// AndroidCaptureDeviceBase is compiled into this project as a linked source file (see the csproj):
// both capture devices in the package (Oboe microphone capture and MediaProjection playback
// capture) hand their buffers to the engine through it, with the arrival time they took
// themselves, and Oboe disconnect recovery restarts the capture timeline through it. The devices
// themselves need Android; this pins down what happens once a buffer reaches the base class.
public sealed class AndroidCaptureDeviceBaseTests
{
    private sealed class TestConfig : DeviceConfig;

    // The smallest engine a device can belong to: the tests never open a device through it.
    private sealed class TestEngine : AudioEngine
    {
        protected override void CleanupBackend() { }
        public override AudioPlaybackDevice InitializePlaybackDevice(DeviceInfo? deviceInfo, AudioFormat format, DeviceConfig config = null) => throw new NotSupportedException();
        public override AudioCaptureDevice InitializeCaptureDevice(DeviceInfo? deviceInfo, AudioFormat format, DeviceConfig config = null) => throw new NotSupportedException();
        public override FullDuplexDevice InitializeFullDuplexDevice(DeviceInfo? playbackDeviceInfo, DeviceInfo? captureDeviceInfo, AudioFormat format, DeviceConfig config = null) => throw new NotSupportedException();
        public override AudioCaptureDevice InitializeLoopbackDevice(AudioFormat format, DeviceConfig config = null) => throw new NotSupportedException();
        public override AudioPlaybackDevice SwitchDevice(AudioPlaybackDevice oldDevice, DeviceInfo newDeviceInfo, DeviceConfig config = null) => throw new NotSupportedException();
        public override AudioCaptureDevice SwitchDevice(AudioCaptureDevice oldDevice, DeviceInfo newDeviceInfo, DeviceConfig config = null) => throw new NotSupportedException();
        public override FullDuplexDevice SwitchDevice(FullDuplexDevice oldDevice, DeviceInfo? newPlaybackInfo, DeviceInfo? newCaptureInfo, DeviceConfig config = null) => throw new NotSupportedException();
        public override void UpdateAudioDevicesInfo() { }
    }

    // Starts and stops the way AndroidCaptureDevice does: IsRunning follows the native stream.
    private sealed class TestCaptureDevice : AndroidCaptureDeviceBase
    {
        public TestCaptureDevice(AudioEngine engine, AudioFormat format) : base(engine, format, new TestConfig())
        {
            Capability = Capability.Record;
        }
        public override void Start() => IsRunning = true;
        public override void Stop() => IsRunning = false;
        public override void Dispose() { IsRunning = false; IsDisposed = true; }
    }

    private static readonly AudioFormat Stereo48k = new() { Format = SampleFormat.F32, Channels = 2, SampleRate = 48000 };

    private static long Ticks(double seconds) => (long)(seconds * Stopwatch.Frequency);

    [Fact]
    public void Deliver_stamps_the_buffer_with_the_arrival_time_the_backend_took()
    {
        //Arrange
        using var engine = new TestEngine();
        var device = new TestCaptureDevice(engine, Stereo48k);
        device.Start();

        //Act - 480 stereo frames (960 samples) = 10 ms, arriving at t = 10 s
        device.Deliver(new float[960], Ticks(10));

        //Assert
        var stamp = device.LastCaptureTimestamp;
        stamp.ArrivalTimestamp.Should().Be(Ticks(10));
        stamp.FrameIndex.Should().Be(0);
        stamp.FrameCount.Should().Be(480);
        stamp.SampleRate.Should().Be(48000);
        stamp.IsDiscontinuity.Should().BeTrue();
        stamp.LatencyTicks.Should().Be(0);
        stamp.Timestamp.Should().Be(Ticks(10) - Ticks(0.010));
    }

    [Fact]
    public void Deliver_raises_OnAudioProcessed_then_OnAudioCaptured_with_the_same_buffer()
    {
        //Arrange
        using var engine = new TestEngine();
        var device = new TestCaptureDevice(engine, Stereo48k);
        var order = new List<string>();
        AudioCaptureTimestamp captured = default;
        float processedFirst = 0, capturedFirst = 0;
        device.OnAudioProcessed += (samples, capability) =>
        {
            order.Add("processed"); processedFirst = samples[0];
            capability.Should().Be(Capability.Record);
        };
        device.OnAudioCaptured += (samples, capability, timestamp) =>
        {
            order.Add("captured"); capturedFirst = samples[0]; captured = timestamp;
        };
        var buffer = new float[960];
        buffer[0] = 0.5f;

        //Act
        device.Deliver(buffer, Ticks(10));

        //Assert
        order.Should().Equal("processed", "captured");
        processedFirst.Should().Be(0.5f);
        capturedFirst.Should().Be(0.5f);
        captured.Should().Be(device.LastCaptureTimestamp);
    }

    [Fact]
    public void Deliver_keeps_consecutive_buffers_frame_contiguous()
    {
        //Arrange
        using var engine = new TestEngine();
        var device = new TestCaptureDevice(engine, Stereo48k);
        device.Start();
        var stamps = new List<AudioCaptureTimestamp>();
        device.OnAudioCaptured += (_, _, timestamp) => stamps.Add(timestamp);

        //Act - callback jitter on arrival must not show in the timestamps
        device.Deliver(new float[960], Ticks(10.000));
        device.Deliver(new float[960], Ticks(10.013));
        device.Deliver(new float[960], Ticks(10.021));

        //Assert
        stamps.Should().HaveCount(3);
        stamps[1].FrameIndex.Should().Be(480);
        stamps[2].FrameIndex.Should().Be(960);
        stamps[1].IsDiscontinuity.Should().BeFalse();
        stamps[2].IsDiscontinuity.Should().BeFalse();
        stamps[1].Timestamp.Should().Be(stamps[0].EndTimestamp);
        stamps[2].Timestamp.Should().Be(stamps[1].EndTimestamp);
    }

    [Fact]
    public void StreamRestarted_starts_a_fresh_timeline_while_the_device_keeps_running()
    {
        //Arrange
        using var engine = new TestEngine();
        var device = new TestCaptureDevice(engine, Stereo48k);
        device.Start();
        device.Deliver(new float[960], Ticks(10.000));
        device.Deliver(new float[960], Ticks(10.010));

        //Act - what Oboe disconnect recovery does before starting the replacement stream
        device.StreamRestarted();
        device.Deliver(new float[960], Ticks(12.000));

        //Assert
        device.IsRunning.Should().BeTrue();
        device.LastCaptureTimestamp.FrameIndex.Should().Be(0);
        device.LastCaptureTimestamp.IsDiscontinuity.Should().BeTrue();
        device.LastCaptureTimestamp.Timestamp.Should().Be(Ticks(12) - Ticks(0.010));
    }

    [Fact]
    public void StreamRestarted_before_any_buffer_is_harmless()
    {
        //Arrange
        using var engine = new TestEngine();
        var device = new TestCaptureDevice(engine, Stereo48k);
        device.Start();

        //Act
        device.StreamRestarted();
        device.Deliver(new float[960], Ticks(10.000));
        device.Deliver(new float[960], Ticks(10.010));

        //Assert
        device.LastCaptureTimestamp.FrameIndex.Should().Be(480);
        device.LastCaptureTimestamp.IsDiscontinuity.Should().BeFalse();
    }

    [Fact]
    public void Stop_then_Start_starts_a_fresh_timeline_without_StreamRestarted()
    {
        //Arrange
        using var engine = new TestEngine();
        var device = new TestCaptureDevice(engine, Stereo48k);
        device.Start();
        device.Deliver(new float[960], Ticks(10.000));
        device.Deliver(new float[960], Ticks(10.010));

        //Act
        device.Stop();
        device.Start();
        device.Deliver(new float[960], Ticks(20.000));

        //Assert
        device.LastCaptureTimestamp.FrameIndex.Should().Be(0);
        device.LastCaptureTimestamp.IsDiscontinuity.Should().BeTrue();
    }

    [Fact]
    public void Deliver_counts_frames_for_mono_capture()
    {
        //Arrange
        using var engine = new TestEngine();
        var device = new TestCaptureDevice(engine, new AudioFormat { Format = SampleFormat.F32, Channels = 1, SampleRate = 44100 });
        device.Start();

        //Act - 441 mono frames = 10 ms
        device.Deliver(new float[441], Ticks(5.000));
        device.Deliver(new float[441], Ticks(5.010));

        //Assert
        device.LastCaptureTimestamp.FrameCount.Should().Be(441);
        device.LastCaptureTimestamp.FrameIndex.Should().Be(441);
        device.LastCaptureTimestamp.SampleRate.Should().Be(44100);
    }
}
