using System;

namespace CodeBrix.Audio.Android;

/// <summary>A snapshot collected outside the audio callback. Counts reset when a stream reopens.</summary>
/// <param name="CallbackCount">Number of native data callbacks.</param>
/// <param name="XRunCount">Hardware underruns/overruns, or -1 if unavailable.</param>
/// <param name="FramesPerBurst">Hardware burst size.</param>
/// <param name="BufferFrames">Current hardware buffer size.</param>
/// <param name="DeviceId">Android device ID selected by the stream.</param>
/// <param name="NativeError">Oboe error code; zero means no native error reported.</param>
/// <param name="CallbackException">The first managed callback failure, if any.</param>
/// <param name="CallbackAllocatedBytes">Managed allocations observed inside callbacks.</param>
/// <param name="MaximumCallbackMicroseconds">Longest observed callback time.</param>
public readonly record struct AndroidAudioDiagnostics(long CallbackCount, int XRunCount,
    int FramesPerBurst, int BufferFrames, int DeviceId, int NativeError, Exception CallbackException,
    long CallbackAllocatedBytes, double MaximumCallbackMicroseconds);

/// <summary>Implemented by Android devices that expose callback and hardware diagnostics.</summary>
public interface IAndroidAudioDevice
{
    /// <summary>Gets a snapshot. Query from a control/UI thread, never the audio callback.</summary>
    AndroidAudioDiagnostics GetDiagnostics();
}
