using System;
using CodeBrix.Audio.Engine.Abstracts;
using CodeBrix.Audio.Engine.Abstracts.Devices;
using CodeBrix.Audio.Engine.Structs;

namespace CodeBrix.Audio.Android.Internal;

// The shared half of this package's capture devices (Oboe microphone capture and MediaProjection
// playback capture): how a captured buffer reaches the engine's base class, which stamps it with
// an AudioCaptureTimestamp before OnAudioProcessed / OnAudioCaptured are raised. Kept free of
// Android types so the host tests can compile it in.
internal abstract class AndroidCaptureDeviceBase : AudioCaptureDevice
{
    protected AndroidCaptureDeviceBase(AudioEngine engine, AudioFormat format, DeviceConfig config)
        : base(engine, format, config) { }

    // Hands one buffer to the base class. arrivalTimestamp is Stopwatch.GetTimestamp() taken as
    // early as the backend can - on entry to the Oboe callback, or as soon as a blocking read
    // returns - so the managed work before this call does not count as capture delay.
    internal void Deliver(Span<float> samples, long arrivalTimestamp) => InvokeOnAudioProcessed(samples, arrivalTimestamp);

    // The native stream was replaced while the device kept running (disconnect / route-change
    // recovery): the new stream's frames do not continue the old timeline, so the next buffer
    // starts it afresh at frame 0, flagged as a discontinuity. Stop() needs no call - the base
    // class resets the clock itself whenever IsRunning becomes false.
    internal void StreamRestarted() => ResetCaptureClock();
}
