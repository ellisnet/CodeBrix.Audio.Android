# Physical-device validation

Initial development requires no attached device. Shipping validation requires both
an ARM64 physical device and an x64 physical device. Do not use an AVD on this
workstation. Listening alone establishes basic function; lifecycle, capture and
sustained synthesis also need verification.

Record device model, ABI, Android/API version, audio route, package/build version,
Debug/Release configuration, duration, diagnostic counters and observed results.
Test API 33 and the newest available supported OS across the device matrix when
hardware is available. Start with the Release diagnostics APK, then compare Debug.

1. Play through speakers, wired/USB headphones and Bluetooth routes available on
   the device. Check channel order, pitch, clipping, underruns and silence on stop.
2. Exercise WAV, MP3, FLAC, Ogg/Vorbis and Opus, including mono/stereo, differing
   source rates, long files, seek-to-zero, mid-file seek, pause/resume and end of file.
3. Exercise application APIs using SharedAudioOutput: overlapping players, shutdown
   and restart, modifiers, mixing, effects and high-level recording/file writers.
4. Run ModestSynth with 1, 32 and 128 voices for at least ten minutes after warmup.
   Record xruns, callback allocations/duration, GC counts, latency and thermal
   behavior. Repeat using the consuming application's actual instrument/mixer graph.
5. Deny, grant and revoke microphone permission. Record and replay; test simultaneous
   playback/capture and long-running clock drift. Verify clean failure on revocation.
6. Plug/unplug routes during playback/capture; switch devices; repeat rapid stop,
   restart and dispose operations. Verify automatic recovery and its disabled mode.
7. Background/foreground, rotate and recreate activities, lose/regain focus and
   interrupt with another audio app/call. The sample is foreground-only; separately
   test the consuming app's foreground-service and notification implementation.
8. Enumerate USB/Bluetooth MIDI hardware, send/receive notes and fragmented SysEx,
   unplug while open, refresh the list and reconnect. Verify no stuck notes or leaks.
9. In an application providing MediaProjection consent and a foreground service,
   capture eligible playback and stop the projection during recording. Verify
   prohibited sources stay excluded. The diagnostics sample does not supply this UI.
10. Exercise SF2, SFZ, DecentSampler, streamed sample libraries, packet/network audio
    and the file writers used by the consuming applications.

The sample provides codec playback/seeking, synthesis workloads, route selection,
microphone recording/replay, focus handling and device/MIDI enumeration. It is a
starting point, not evidence that every scenario above has passed.

| Target | Listening | Capture/lifecycle/routing | Sustained workload |
| --- | --- | --- | --- |
| Physical ARM64 | Pending | Pending | Pending |
| Physical x64 | Pending | Pending | Pending |
