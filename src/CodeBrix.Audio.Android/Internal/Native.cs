using System.Runtime.InteropServices;

namespace CodeBrix.Audio.Android.Internal;

internal static partial class Native
{
    private const string Library = "codebrix_miniaudio";
    [LibraryImport(Library, EntryPoint = "cb_oboe_abi_version")]
    internal static partial int AbiVersion();
    [LibraryImport(Library, EntryPoint = "cb_oboe_open")]
    internal static partial int Open(int input, int device, int rate, int channels, int exclusive,
        int bursts, int usage, int preset, nint callback, nint user, out nint stream);
    [LibraryImport(Library, EntryPoint = "cb_oboe_start")]
    internal static partial int Start(StreamHandle stream);
    [LibraryImport(Library, EntryPoint = "cb_oboe_stop")]
    internal static partial int Stop(StreamHandle stream);
    [LibraryImport(Library, EntryPoint = "cb_oboe_close")]
    internal static partial void Close(nint stream);
    [LibraryImport(Library, EntryPoint = "cb_oboe_error")]
    internal static partial int Error(StreamHandle stream);
    [LibraryImport(Library, EntryPoint = "cb_oboe_callbacks")]
    internal static partial long Callbacks(StreamHandle stream);
    [LibraryImport(Library, EntryPoint = "cb_oboe_xruns")]
    internal static partial int XRuns(StreamHandle stream);
    [LibraryImport(Library, EntryPoint = "cb_oboe_burst")]
    internal static partial int Burst(StreamHandle stream);
    [LibraryImport(Library, EntryPoint = "cb_oboe_buffer")]
    internal static partial int Buffer(StreamHandle stream);
    [LibraryImport(Library, EntryPoint = "cb_oboe_device")]
    internal static partial int Device(StreamHandle stream);
}

internal sealed class StreamHandle : SafeHandle
{
    private GCHandle _owner;
    internal StreamHandle(nint stream, GCHandle owner) : base(0, true) { SetHandle(stream); _owner = owner; }
    public override bool IsInvalid => handle == 0;
    protected override bool ReleaseHandle()
    {
        Native.Close(handle);
        if (_owner.IsAllocated) _owner.Free();
        return true;
    }
}
