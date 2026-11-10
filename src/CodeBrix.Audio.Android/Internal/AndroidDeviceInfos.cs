using System;
using System.Globalization;
using CodeBrix.Audio.Engine.Structs;

namespace CodeBrix.Audio.Android.Internal;

// Builds the DeviceInfo values AndroidAudioEngine reports for its playback and capture lists.
// Kept free of Android types so the host tests can compile it in: the engine passes the
// AudioDeviceInfo id and display name, never the AudioDeviceInfo itself.
internal static class AndroidDeviceInfos
{
    // The OsDeviceIdBackend name for ids taken from Android's AudioDeviceInfo.getId(). That id is
    // the one AudioManager, AudioRecord / AudioTrack routing and AAudio / Oboe all use for a device.
    internal const string OsDeviceIdBackend = "android";

    // The synthetic first entry: Id 0 lets Android pick the route. It names no OS device, so it
    // carries no OsDeviceId (an application that wants the routed device's id reads
    // AndroidAudioDiagnostics.DeviceId from the open stream).
    internal static DeviceInfo SystemDefault() =>
        new() { Id = 0, Name = "System default", IsDefault = true, SupportedDataFormats = [] };

    // One AudioDeviceInfo. SupportedDataFormats stays empty on purpose (see MAINTAINER-README.txt).
    internal static DeviceInfo ForDevice(int androidDeviceId, string name) => new()
    {
        Id = androidDeviceId,
        Name = name,
        IsDefault = false,
        SupportedDataFormats = [],
        OsDeviceId = androidDeviceId.ToString(CultureInfo.InvariantCulture),
        OsDeviceIdBackend = OsDeviceIdBackend
    };
}
