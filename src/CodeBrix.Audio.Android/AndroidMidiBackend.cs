using CodeBrix.Audio.Android.Internal;
using CodeBrix.Audio.Engine.Abstracts;
using CodeBrix.Audio.Engine.Interfaces;
using CodeBrix.Audio.Engine.Midi.Devices;
using CodeBrix.Audio.Engine.Midi.Structs;
using CodeBrix.Audio.Engine.Structs;
using global::Android.Content;
using global::Android.Media.Midi;
using global::Android.OS;
using NativeMidiDevice = global::Android.Media.Midi.MidiDevice;
using NativeMidiInfo = global::Android.Media.Midi.MidiDeviceInfo;
using MidiDeviceInfo = CodeBrix.Audio.Engine.Midi.Structs.MidiDeviceInfo;
using EngineMidiDevice = CodeBrix.Audio.Engine.Midi.Abstracts.MidiDevice;
using Result = CodeBrix.Audio.Engine.Structs.Result;

namespace CodeBrix.Audio.Android;

/// <summary>Android MIDI 1.0 byte-stream ports exposed through the shared engine MIDI API.</summary>
/// <remarks>USB, virtual and already-connected Bluetooth MIDI devices are enumerated. The app
/// controls Bluetooth discovery/pairing and its permissions. IDs are stable for this backend's
/// lifetime; timestamps are Android monotonic nanoseconds. Open ports from a control thread.</remarks>
public sealed class AndroidMidiBackend : IMidiBackend
{
    private readonly global::Android.Media.Midi.MidiManager _manager;
    private readonly HandlerThread _thread;
    private readonly Handler _handler;
    private readonly object _gate = new();
    private readonly Dictionary<(int Device, int Port, MidiPortType Type), int> _ids = [];
    private readonly Dictionary<int, (NativeMidiInfo Device, int Port, MidiPortType Type)> _ports = [];
    private readonly HashSet<EngineMidiDevice> _opened = [];
    private int _nextId;
    private bool _disposed;

    /// <summary>Creates a backend with a dedicated device-open callback thread.</summary>
    /// <param name="context">Android application or activity context.</param>
    public AndroidMidiBackend(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _manager = (global::Android.Media.Midi.MidiManager?)context.ApplicationContext?.GetSystemService(Context.MidiService)
            ?? throw new PlatformNotSupportedException("Android MIDI is unavailable on this device.");
        _thread = new HandlerThread("CodeBrix MIDI control"); _thread.Start();
        _handler = new Handler(_thread.Looper!);
    }
    /// <summary>The most recent receive/parser or subscriber failure, captured outside JNI.</summary>
    public Exception? LastReceiveError { get; private set; }
    /// <inheritdoc />
    public void Initialize(AudioEngine engine) { ArgumentNullException.ThrowIfNull(engine); }
    /// <inheritdoc />
    public void UpdateMidiDevicesInfo(out MidiDeviceInfo[] inputs, out MidiDeviceInfo[] outputs)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var receive = new List<MidiDeviceInfo>(); var send = new List<MidiDeviceInfo>();
            _ports.Clear();
            foreach (var device in _manager.GetDevicesForTransport((int)MidiTransport.MidiByteStream) ?? [])
            foreach (var port in device.GetPorts() ?? [])
            {
                var key = (device.Id, port.PortNumber, port.Type);
                if (!_ids.TryGetValue(key, out int id)) _ids.Add(key, id = ++_nextId);
                _ports[id] = (device, port.PortNumber, port.Type);
                var info = new MidiDeviceInfo { Id = id, Name = $"{device.Properties?.GetString(NativeMidiInfo.PropertyName) ?? "MIDI"} / {port.Name ?? port.PortNumber.ToString()}" };
                // Android's OUTPUT port is an INPUT to our application, and vice versa.
                (port.Type == MidiPortType.Output ? receive : send).Add(info);
            }
            inputs = receive.ToArray(); outputs = send.ToArray();
        }
    }
    /// <inheritdoc />
    public MidiInputDevice CreateMidiInputDevice(MidiDeviceInfo deviceInfo)
    {
        OboeStream.AssertControlThread();
        lock (_gate)
        {
            var (device, port) = Open(deviceInfo, MidiPortType.Output);
            try
            {
                var result = new Input(deviceInfo, device, port, ex => LastReceiveError = ex, Remove);
                _opened.Add(result); return result;
            }
            catch { device.Close(); device.Dispose(); throw; }
        }
    }
    /// <inheritdoc />
    public MidiOutputDevice CreateMidiOutputDevice(MidiDeviceInfo deviceInfo)
    {
        OboeStream.AssertControlThread();
        lock (_gate)
        {
            var (device, port) = Open(deviceInfo, MidiPortType.Input);
            try { var result = new Output(deviceInfo, device, port, Remove); _opened.Add(result); return result; }
            catch { device.Close(); device.Dispose(); throw; }
        }
    }
    private (NativeMidiDevice, int) Open(MidiDeviceInfo info, MidiPortType type)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_ports.TryGetValue(info.Id, out var port) || port.Type != type)
            throw new ArgumentException("MIDI port is no longer available. Refresh the engine's MIDI device list.", nameof(info));
        var opened = new OpenListener();
        _manager.OpenDevice(port.Device, opened, _handler);
        try
        {
            var device = opened.Completion.Task.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult()
                ?? throw new IOException("Android refused to open the MIDI device.");
            return (device, port.Port);
        }
        catch
        {
            if (!opened.Completion.TrySetCanceled() && opened.Completion.Task.IsCompletedSuccessfully)
            {
                var late = opened.Completion.Task.Result; late?.Close(); late?.Dispose();
            }
            throw;
        }
    }
    private void Remove(EngineMidiDevice device) { lock (_gate) _opened.Remove(device); }
    /// <summary>Closes owned MIDI ports and the control thread.</summary>
    public void Dispose()
    {
        EngineMidiDevice[] opened;
        lock (_gate) { if (_disposed) return; _disposed = true; opened = _opened.ToArray(); _opened.Clear(); }
        foreach (var device in opened) device.Dispose();
        _thread.QuitSafely(); _thread.Join(); _handler.Dispose(); _thread.Dispose();
    }
    private sealed class OpenListener : Java.Lang.Object, global::Android.Media.Midi.MidiManager.IOnDeviceOpenedListener
    {
        internal readonly TaskCompletionSource<NativeMidiDevice?> Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void OnDeviceOpened(NativeMidiDevice? device)
        {
            if (!Completion.TrySetResult(device)) { device?.Close(); device?.Dispose(); }
            Dispose();
        }
    }
    private sealed class Input : MidiInputDevice
    {
        private readonly NativeMidiDevice _device;
        private readonly MidiOutputPort _port;
        private readonly Receiver _receiver;
        private readonly Action<EngineMidiDevice> _removed;
        internal Input(MidiDeviceInfo info, NativeMidiDevice device, int port, Action<Exception> error, Action<EngineMidiDevice> removed) : base(info)
        {
            _device = device; _removed = removed;
            _port = device.OpenOutputPort(port) ?? throw new IOException("Android MIDI output port is busy or unavailable.");
            _receiver = new Receiver(new MidiByteParser(InvokeOnMessageReceived, InvokeOnSysExReceived), error);
            _port.Connect(_receiver);
        }
        public override void Dispose()
        {
            if (IsDisposed) return; IsDisposed = true;
            _port.Disconnect(_receiver); _port.Close(); _port.Dispose(); _receiver.Dispose();
            _device.Close(); _device.Dispose(); _removed(this);
        }
    }
    private sealed class Receiver(MidiByteParser parser, Action<Exception> error) : MidiReceiver
    {
        public override void OnSend(byte[]? msg, int offset, int count, long timestamp)
        {
            if (msg == null) return;
            try { parser.Feed(msg.AsSpan(offset, count), timestamp); }
            catch (Exception ex) { error(ex); }
        }
    }
    private sealed class Output : MidiOutputDevice
    {
        private readonly NativeMidiDevice _device;
        private readonly MidiInputPort _port;
        private readonly byte[] _short = new byte[3];
        private readonly object _gate = new();
        private readonly Action<EngineMidiDevice> _removed;
        internal Output(MidiDeviceInfo info, NativeMidiDevice device, int port, Action<EngineMidiDevice> removed) : base(info)
        {
            _device = device; _removed = removed;
            _port = device.OpenInputPort(port) ?? throw new IOException("Android MIDI input port is busy or unavailable.");
        }
        public override Result SendMessage(MidiMessage message)
        {
            lock (_gate)
            {
                if (IsDisposed) return Result.Fail(new Error("The MIDI output is closed."));
                if (message.StatusByte < 0x80 || message.StatusByte == 0xF0)
                    return Result.Fail(new ValidationError("Use a MIDI status byte; send SysEx with SendSysEx."));
                _short[0] = message.StatusByte; _short[1] = message.Data1; _short[2] = message.Data2;
                try { _port.Send(_short, 0, 1 + MidiByteParser.DataBytes(message.StatusByte), message.Timestamp); return Result.Ok(); }
                catch (Exception ex) { return Result.Fail(new IoError("MIDI send", ex)); }
            }
        }
        public override Result SendSysEx(byte[] data)
        {
            ArgumentNullException.ThrowIfNull(data);
            lock (_gate)
            {
                if (IsDisposed) return Result.Fail(new Error("The MIDI output is closed."));
                try
                {
                    var payload = new byte[checked(data.Length + 2)]; payload[0] = 0xF0; payload[^1] = 0xF7;
                    data.CopyTo(payload, 1);
                    _port.Send(payload, 0, payload.Length); // MidiReceiver splits to MaxMessageSize.
                    return Result.Ok();
                }
                catch (Exception ex) { return Result.Fail(new IoError("MIDI SysEx send", ex)); }
            }
        }
        public override void Dispose()
        {
            lock (_gate)
            {
                if (IsDisposed) return; IsDisposed = true;
                _port.Close(); _port.Dispose(); _device.Close(); _device.Dispose();
            }
            _removed(this);
        }
    }
}
