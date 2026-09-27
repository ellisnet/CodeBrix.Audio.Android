using System;
using CodeBrix.Audio.Engine.Abstracts.Devices;
using global::Android.Media;

namespace CodeBrix.Audio.Android;

/// <summary>Options for Oboe streams. Configure before creating a device.</summary>
public sealed class AndroidDeviceConfig : DeviceConfig
{
    /// <summary>Requests exclusive low-latency access; Android may grant shared access instead.</summary>
    public bool PreferExclusive { get; init; } = true;
    /// <summary>Requested buffer size in hardware bursts, between one and sixteen.</summary>
    public int BufferBursts { get; init; } = 2;
    /// <summary>Android playback usage. Media is appropriate for music playback.</summary>
    public AudioUsageKind Usage { get; init; } = AudioUsageKind.Media;
    /// <summary>Capture processing preset. Unprocessed avoids automatic voice processing when supported.</summary>
    public AudioSource InputPreset { get; init; } = AudioSource.Unprocessed;
    /// <summary>Reopens disconnected streams on the default route, preserving the managed graph.</summary>
    public bool RecoverDisconnectedStreams { get; init; } = true;

    internal void Validate()
    {
        if (BufferBursts is < 1 or > 16) throw new ArgumentOutOfRangeException(nameof(BufferBursts));
    }
}
