using System;
using System.Collections.Generic;
using System.Linq;
using CodeBrix.Audio.Android.Internal;
using CodeBrix.Audio.Engine.Midi.Structs;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Audio.Android.Tests;

// MidiByteParser is compiled into this project as a linked source file (see the csproj):
// Android MIDI callbacks may split a message anywhere or combine many messages, and the
// parser has to keep running status and interleaved real-time bytes straight across them.
public sealed class MidiByteParserTests
{
    [Fact]
    public void Feed_keeps_running_status_consistent_when_a_subscriber_throws()
    {
        //Arrange
        bool fail = true;
        var received = new List<MidiMessage>();
        var parser = new MidiByteParser(value =>
        {
            if (fail) throw new InvalidOperationException("Subscriber failed.");
            received.Add(value);
        }, _ => { });
        Action failingFeed = () => parser.Feed([0x90, 60, 100], 1);

        //Act
        failingFeed.Should().Throw<InvalidOperationException>();
        fail = false;
        parser.Feed([61, 101], 2);

        //Assert
        var note = received.Should().ContainSingle().Which;
        note.StatusByte.Should().Be((byte)0x90);
        note.Data1.Should().Be((byte)61);
        note.Data2.Should().Be((byte)101);
        note.Timestamp.Should().Be(2);
    }

    [Fact]
    public void Feed_survives_fragmentation_running_status_and_interleaved_clock()
    {
        //Arrange
        var messages = new List<MidiMessage>();
        var sysex = new List<byte[]>();
        var parser = new MidiByteParser(messages.Add, sysex.Add);

        //Act
        parser.Feed([0x90, 60], 1);
        parser.Feed([0xF8, 100, 61, 101, 0xC0], 2);
        parser.Feed([12, 13, 0xF0, 1, 2, 0xF8, 3], 3);
        parser.Feed([4, 0xF7, 0x80, 60, 0], 4);

        //Assert
        messages.Select(m => m.StatusByte).Should().Equal(new byte[] { 0xF8, 0x90, 0x90, 0xC0, 0xC0, 0xF8, 0x80 });
        messages[1].Data1.Should().Be((byte)60);
        messages[1].Data2.Should().Be((byte)100);
        messages[4].Data1.Should().Be((byte)13);
        sysex.Should().ContainSingle().Which.Should().Equal(new byte[] { 1, 2, 3, 4 });
    }

    [Fact]
    public void Feed_cancels_running_status_on_system_common_and_recovers_from_an_interrupted_sysex()
    {
        //Arrange
        var messages = new List<MidiMessage>();
        var sysex = new List<byte[]>();
        var parser = new MidiByteParser(messages.Add, sysex.Add);

        //Act
        parser.Feed([0x90, 60, 100, 0xF2, 1, 2, 61, 100, 0xF0, 4, 0x91, 62, 90], 42);

        //Assert
        messages.Select(m => m.StatusByte).Should().Equal(new byte[] { 0x90, 0xF2, 0x91 });
        sysex.Should().BeEmpty();
        messages.Select(m => m.Timestamp).Should().Equal(new long[] { 42, 42, 42 });
    }

    [Theory]
    [InlineData(0x90, 2)]
    [InlineData(0xC0, 1)]
    [InlineData(0xE0, 2)]
    [InlineData(0xF1, 1)]
    [InlineData(0xF2, 2)]
    [InlineData(0xF8, 0)]
    public void DataBytes_returns_the_MIDI_1_0_data_length_for_each_status(int status, int expected)
        => MidiByteParser.DataBytes((byte)status).Should().Be(expected);
}
