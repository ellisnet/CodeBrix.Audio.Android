using CodeBrix.Audio.Engine.Midi.Structs;

namespace CodeBrix.Audio.Android.Internal;

// Android MIDI callbacks may split a message anywhere or combine many messages. Running
// status and realtime messages interleaved inside SysEx must survive those boundaries.
internal sealed class MidiByteParser(Action<MidiMessage> message, Action<byte[]> sysex)
{
    private byte _status;
    private byte _first;
    private int _received;
    private int _needed;
    private List<byte>? _sysex;
    internal void Feed(ReadOnlySpan<byte> bytes, long timestamp)
    {
        foreach (byte value in bytes)
        {
            if (value >= 0xF8) { message(new(value, 0, 0, timestamp)); continue; }
            if (_sysex != null)
            {
                if (value == 0xF7) { var data = _sysex.ToArray(); _sysex = null; sysex(data); continue; }
                if (value < 0x80)
                {
                    if (_sysex.Count >= 1_048_576) { _sysex = null; throw new InvalidDataException("MIDI SysEx exceeded the 1 MiB safety limit."); }
                    _sysex.Add(value); continue;
                }
                _sysex = null; // A new status cancels an incomplete SysEx.
            }
            if (value >= 0x80)
            {
                _received = 0; _status = value; _needed = DataBytes(value);
                if (value == 0xF0) { _status = 0; _sysex = []; continue; }
                if (_needed == 0) { _status = 0; message(new(value, 0, 0, timestamp)); }
                continue;
            }
            if (_status == 0) continue;
            if (_received++ == 0) _first = value;
            if (_received < _needed) continue;
            var completed = new MidiMessage(_status, _first, _needed == 2 ? value : (byte)0, timestamp);
            _received = 0;
            if (_status >= 0xF0) _status = 0; // Running status applies only to channel messages.
            message(completed); // State is consistent even if an application subscriber throws.
        }
    }
    internal static int DataBytes(byte status) => status switch
    {
        >= 0x80 and <= 0xBF => 2,
        >= 0xC0 and <= 0xDF => 1,
        >= 0xE0 and <= 0xEF => 2,
        0xF1 or 0xF3 => 1,
        0xF2 => 2,
        _ => 0
    };
}
