using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CodeBrix.Audio.Engine.Abstracts;
using CodeBrix.Audio.Engine.Enums;
using CodeBrix.Audio.Engine.Structs;

namespace CodeBrix.Audio.Android.Internal;

internal sealed unsafe class OboeStream : IDisposable
{
    [ThreadStatic] private static bool _inCallback;
    private readonly AudioProcessCallback _process;
    private readonly bool _input;
    private readonly AudioFormat _format;
    private readonly AndroidDeviceConfig _config;
    private readonly object _gate = new();
    private StreamHandle _handle;
    private Exception? _callbackException;
    private long _allocatedBytes;
    private long _maxTicks;
    private bool _disposed;

    internal OboeStream(bool input, int deviceId, AudioFormat format, AndroidDeviceConfig config, AudioProcessCallback process)
    {
        _input = input; _format = format; _config = config; _process = process;
        _handle = Open(deviceId);
    }

    private StreamHandle Open(int deviceId)
    {
        var owner = GCHandle.Alloc(this, GCHandleType.Weak);
        try
        {
            int result = Native.Open(_input ? 1 : 0, deviceId, _format.SampleRate, _format.Channels,
                _config.PreferExclusive ? 1 : 0, _config.BufferBursts, (int)_config.Usage, (int)_config.InputPreset,
                (nint)(delegate* unmanaged[Cdecl]<nint, float*, int, int>)&Render,
                GCHandle.ToIntPtr(owner), out var pointer);
            Check(result, "open");
            return new StreamHandle(pointer, owner);
        }
        catch { owner.Free(); throw; }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int Render(nint owner, float* data, int frames)
    {
        // Native output is already zeroed. A weak handle avoids leaking a forgotten device:
        // StreamHandle's finalizer closes native IO before releasing the GC handle.
        var target = GCHandle.FromIntPtr(owner).Target as OboeStream;
        if (target == null) return 1;
        _inCallback = true;
        long started = Stopwatch.GetTimestamp();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        try
        {
            var samples = new Span<float>(data, checked(frames * target._format.Channels));
            target._process(samples, target._input ? Capability.Record : Capability.Playback);
            return 0;
        }
        catch (Exception ex)
        {
            if (!target._input) new Span<float>(data, frames * target._format.Channels).Clear();
            Interlocked.CompareExchange(ref target._callbackException, ex, null);
            return 1; // Never allow a managed exception across the native ABI.
        }
        finally
        {
            Interlocked.Add(ref target._allocatedBytes, GC.GetAllocatedBytesForCurrentThread() - allocated);
            long elapsed = Stopwatch.GetTimestamp() - started;
            long previous = Interlocked.Read(ref target._maxTicks);
            if (elapsed > previous) Interlocked.Exchange(ref target._maxTicks, elapsed);
            _inCallback = false;
        }
    }

    internal static void AssertControlThread()
    {
        if (_inCallback) throw new InvalidOperationException("Start, stop, dispose and device management must run outside the audio callback.");
    }
    internal static void EnterCaptureCallback() => _inCallback = true;
    internal static void ExitCaptureCallback() => _inCallback = false;
    internal void Start()
    {
        AssertControlThread();
        lock (_gate) { ObjectDisposedException.ThrowIf(_disposed, this); Check(Native.Start(_handle), "start"); }
    }
    internal void Stop()
    {
        AssertControlThread();
        lock (_gate) { if (!_disposed) Native.Stop(_handle); }
    }
    internal bool TryRecover()
    {
        AssertControlThread();
        lock (_gate)
        {
            if (_disposed || Native.Error(_handle) == 0 || !_config.RecoverDisconnectedStreams) return false;
            // Keep the disconnected handle and its error until restart succeeds, so failures
            // can be retried on the next control tick without losing their diagnostics.
            Native.Stop(_handle);
            var replacement = Open(0);
            try { Check(Native.Start(replacement), "restart"); }
            catch { replacement.Dispose(); throw; }
            _handle.Dispose();
            _handle = replacement;
            return true;
        }
    }
    internal AndroidAudioDiagnostics Diagnostics()
    {
        AssertControlThread();
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return new(Native.Callbacks(_handle), Native.XRuns(_handle), Native.Burst(_handle),
                Native.Buffer(_handle), Native.Device(_handle), Native.Error(_handle),
                Volatile.Read(ref _callbackException), Interlocked.Read(ref _allocatedBytes),
                Interlocked.Read(ref _maxTicks) * 1_000_000d / Stopwatch.Frequency);
        }
    }
    public void Dispose()
    {
        AssertControlThread();
        lock (_gate) { if (_disposed) return; _disposed = true; _handle.Dispose(); }
    }
    private static void Check(int result, string operation)
    {
        if (result != 0) throw new InvalidOperationException($"Oboe {operation} failed with native error {result}.");
    }
}
