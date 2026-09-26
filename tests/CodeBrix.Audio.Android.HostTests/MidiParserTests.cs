using CodeBrix.Audio.Android.Internal;
using CodeBrix.Audio.Engine.Midi.Structs;
using Xunit;

namespace CodeBrix.Audio.Android.HostTests;

public sealed class MidiParserTests
{
    [Fact]
    public void subscriber_failure_does_not_corrupt_running_status_for_the_next_callback()
    {
        //Arrange
        bool fail = true;
        var received = new List<MidiMessage>();
        var parser = new MidiByteParser(value =>
        {
            if (fail) throw new InvalidOperationException("Subscriber failed.");
            received.Add(value);
        }, _ => { });

        //Act
        Assert.Throws<InvalidOperationException>(() => parser.Feed([0x90, 60, 100], 1));
        fail = false;
        parser.Feed([61, 101], 2);

        //Assert
        var note = Assert.Single(received);
        Assert.Equal((byte)0x90, note.StatusByte);
        Assert.Equal((byte)61, note.Data1);
        Assert.Equal((byte)101, note.Data2);
        Assert.Equal(2, note.Timestamp);
    }
    [Fact]
    public void messages_survive_fragmentation_running_status_and_interleaved_clock()
    {
        //Arrange
        var messages = new List<MidiMessage>(); var sysex = new List<byte[]>();
        var parser = new MidiByteParser(messages.Add, sysex.Add);

        //Act
        parser.Feed([0x90, 60], 1);
        parser.Feed([0xF8, 100, 61, 101, 0xC0], 2);
        parser.Feed([12, 13, 0xF0, 1, 2, 0xF8, 3], 3);
        parser.Feed([4, 0xF7, 0x80, 60, 0], 4);

        //Assert
        Assert.Equal(new byte[] { 0xF8, 0x90, 0x90, 0xC0, 0xC0, 0xF8, 0x80 }, messages.Select(m => m.StatusByte));
        Assert.Equal((byte)60, messages[1].Data1);
        Assert.Equal((byte)100, messages[1].Data2);
        Assert.Equal((byte)13, messages[4].Data1);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, Assert.Single(sysex));
    }
    [Fact]
    public void system_common_cancels_running_status_and_an_interrupted_sysex_recovers()
    {
        //Arrange
        var messages = new List<MidiMessage>(); var sysex = new List<byte[]>();
        var parser = new MidiByteParser(messages.Add, sysex.Add);

        //Act
        parser.Feed([0x90, 60, 100, 0xF2, 1, 2, 61, 100, 0xF0, 4, 0x91, 62, 90], 42);

        //Assert
        Assert.Equal(new byte[] { 0x90, 0xF2, 0x91 }, messages.Select(m => m.StatusByte));
        Assert.Empty(sysex);
        Assert.All(messages, m => Assert.Equal(42, m.Timestamp));
    }
}
