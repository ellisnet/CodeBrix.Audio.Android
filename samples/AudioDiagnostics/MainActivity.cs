using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using CodeBrix.Audio.Android;
using CodeBrix.Audio.Engine.Abstracts;
using CodeBrix.Audio.Engine.Abstracts.Devices;
using CodeBrix.Audio.Engine.Components;
using CodeBrix.Audio.Engine.Providers;
using CodeBrix.Audio.ModestSynth;
using CodeBrix.Audio.Opus;
using CodeBrix.Audio.Wave;
using global::Android.App;
using global::Android.Content.PM;
using global::Android.Media;
using global::Android.OS;
using global::Android.Widget;
using AudioFormat = CodeBrix.Audio.Engine.Structs.AudioFormat;
using SoundPlayer = CodeBrix.Audio.Engine.Components.SoundPlayer;
using AndroidBuild = global::Android.OS.Build;

namespace CodeBrix.Audio.Diagnostics;

[Activity(Label = "CodeBrix Audio Diagnostics", MainLauncher = true, Exported = true)]
public sealed class MainActivity : Activity
{
    private AndroidAudioEngine _engine = null;
    private AndroidAudioFocus _focus = null;
    private AudioPlaybackDevice _playback;
    private AudioCaptureDevice _capture;
    private SoundComponent _voice;
    private TextView _status = null;
    private TextView _metrics = null;
    private Timer _timer;
    private readonly float[] _recorded = new float[48000 * 2 * 10];
    private int _recordedCount;
    private int _route;

    protected override void OnCreate(Bundle savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        CodeBrixAndroidAudio.Initialize(this);
        CodeBrix.Audio.ModestSynth.ModestSynth.Register();
        CodeBrixAudioOpus.Register();
        _engine = new AndroidAudioEngine(this);
        CodeBrixAudioOpus.Register(_engine);
        _focus = new AndroidAudioFocus(this);
        _focus.FocusChanged += change => { if (change < AudioFocus.None) StopAll(); };
        var panel = new LinearLayout(this) { Orientation = global::Android.Widget.Orientation.Vertical };
        _status = new TextView(this) { Text = "Ready. Use a low device volume for the first listening test." };
        _metrics = new TextView(this);
        panel.AddView(_status); panel.AddView(_metrics);
        AddButton(panel, "ModestSynth: 1 voice", () => PlaySynth(1));
        AddButton(panel, "ModestSynth: 32 voices", () => PlaySynth(32));
        AddButton(panel, "ModestSynth: 128 voices", () => PlaySynth(128));
        foreach (string extension in new[] { "wav", "mp3", "flac", "ogg", "opus" })
        {
            string codec = extension;
            AddButton(panel, $"Decode / seek / play {codec.ToUpperInvariant()}", () => PlayFixture(codec));
        }
        AddButton(panel, "Extract FLAC asset to storage, play by path", PlayMaterializedAsset);
        AddButton(panel, "Pause / resume", () => { if (_playback?.IsRunning == true) _playback.Stop(); else StartPlayback(); });
        AddButton(panel, "Seek file to start", () => { if (_voice is SoundPlayer player) { _playback?.Stop(); player.Seek(0); StartPlayback(); } });
        AddButton(panel, "Next output route", NextRoute);
        AddButton(panel, "Record microphone (up to 10 seconds)", StartCapture);
        AddButton(panel, "Play captured microphone", PlayCapture);
        AddButton(panel, "Refresh audio / MIDI devices", ListDevices);
        AddButton(panel, "List packet codecs (Core + add-ons)", ListPacketCodecs);
        AddButton(panel, "Stop all", StopAll);
        var scroll = new ScrollView(this); scroll.AddView(panel); SetContentView(scroll);
        _timer = new Timer(_ => RunOnUiThread(UpdateMetrics), null, 500, 500);
    }
    private void AddButton(LinearLayout panel, string text, Action action)
    {
        var button = new Button(this) { Text = text };
        button.Click += (_, _) =>
        {
            try { action(); }
            catch (Exception ex) { _status.Text = ex.ToString(); }
        };
        panel.AddView(button);
    }
    private void EnsurePlayback() => _playback ??= _engine.InitializePlaybackDevice(null, AudioFormat.DvdHq);
    private void StartPlayback()
    {
        EnsurePlayback();
        if (!_focus.Request()) throw new InvalidOperationException("Android denied audio focus.");
        _playback.Start();
    }
    private void ReplaceVoice(SoundComponent voice)
    {
        EnsurePlayback(); _playback.Stop();
        if (_voice != null) { _playback.MasterMixer.RemoveComponent(_voice); _voice.Dispose(); }
        _voice = voice; _playback.MasterMixer.AddComponent(voice); StartPlayback();
    }
    private void PlaySynth(int voices)
    {
        ReplaceVoice(new SynthVoice(_engine, voices));
        _status.Text = $"ModestSynth: {voices} simultaneous voices. Watch callback time, allocations and underruns.";
    }
    private void PlayFixture(string codec)
    {
        // These short synthetic assets are decoded before playback. No file IO or decoding
        // runs in the device callback; long-file apps should use a bounded producer queue.
        using var source = Assets.Open($"tone.{codec}");
        using var compressed = new MemoryStream(); source.CopyTo(compressed); compressed.Position = 0;
        using var decoder = _engine.CreateDecoder(compressed, codec == "opus" ? "ogg" : codec, AudioFormat.DvdHq);
        var block = new float[4096]; var samples = new List<float>();
        while (true)
        {
            int read = decoder.Decode(block); if (read == 0) break;
            samples.AddRange(block.AsSpan(0, read).ToArray());
            if (samples.Count > 48000 * 2 * 30) throw new InvalidDataException("Diagnostic fixture exceeds 30 seconds.");
        }
        if (samples.Count == 0 || !decoder.Seek(0) || decoder.Decode(block) == 0)
            throw new InvalidDataException("Decoder/seek verification failed.");
        var player = new SoundPlayer(_engine, AudioFormat.DvdHq, new RawDataProvider(samples.ToArray())) { IsLooping = true, Volume = 0.25f };
        player.Play(); ReplaceVoice(player);
        _status.Text = $"{codec}: decoded {samples.Count / 2} frames; seek passed; looping PCM playback.";
    }
    private void PlayMaterializedAsset()
    {
        // The route a packaged sample library takes on Android: the asset is copied out of the APK
        // once per installed build and then opened by PATH, as the instrument libraries require.
        bool wasMaterialized = AndroidPackagedAssets.IsMaterialized("tone.flac");
        string path = AndroidPackagedAssets.Materialize("tone.flac");
        using var file = File.OpenRead(path);
        using var decoder = _engine.CreateDecoder(file, "flac", AudioFormat.DvdHq);
        var block = new float[4096]; var samples = new List<float>();
        int read;
        while ((read = decoder.Decode(block)) > 0) samples.AddRange(block.AsSpan(0, read).ToArray());
        var player = new SoundPlayer(_engine, AudioFormat.DvdHq, new RawDataProvider(samples.ToArray())) { IsLooping = true, Volume = 0.25f };
        player.Play(); ReplaceVoice(player);
        _status.Text = $"{(wasMaterialized ? "Reused" : "Extracted")} {path} ({new FileInfo(path).Length} bytes); playing from the file.";
    }
    private void StartCapture()
    {
        if (CheckSelfPermission(global::Android.Manifest.Permission.RecordAudio) != Permission.Granted)
        {
            RequestPermissions([global::Android.Manifest.Permission.RecordAudio], 1);
            _status.Text = "Grant microphone permission, then press Record again."; return;
        }
        _capture?.Dispose(); Volatile.Write(ref _recordedCount, 0);
        _capture = _engine.InitializeCaptureDevice(null, AudioFormat.DvdHq);
        _capture.OnAudioProcessed += (samples, _) =>
        {
            int offset = Volatile.Read(ref _recordedCount);
            int count = Math.Min(samples.Length, _recorded.Length - offset);
            samples[..count].CopyTo(_recorded.AsSpan(offset));
            Volatile.Write(ref _recordedCount, offset + count);
        };
        _capture.Start(); _status.Text = "Recording microphone. Playback can continue for a duplex test.";
    }
    private void PlayCapture()
    {
        _capture?.Stop();
        var samples = _recorded.AsSpan(0, Volatile.Read(ref _recordedCount)).ToArray();
        if (samples.Length == 0) throw new InvalidOperationException("Record microphone audio first.");
        var player = new SoundPlayer(_engine, AudioFormat.DvdHq, new RawDataProvider(samples)) { Volume = 0.5f };
        player.Play(); ReplaceVoice(player); _status.Text = "Playing captured microphone audio.";
    }
    private void NextRoute()
    {
        EnsurePlayback(); _engine.UpdateAudioDevicesInfo();
        var devices = _engine.PlaybackDevices; _route = (_route + 1) % devices.Length;
        _playback = _engine.SwitchDevice(_playback, devices[_route]);
        _status.Text = $"Requested output: {devices[_route].Name}. Android routing policy may override the request.";
    }
    private void ListDevices()
    {
        _engine.UpdateAudioDevicesInfo(); _engine.UpdateMidiDevicesInfo();
        _status.Text = "Outputs: " + string.Join(", ", _engine.PlaybackDevices.Select(d => d.Name))
            + "\nInputs: " + string.Join(", ", _engine.CaptureDevices.Select(d => d.Name))
            + "\nMIDI inputs: " + string.Join(", ", _engine.MidiInputDevices.Select(d => d.Name))
            + "\nMIDI outputs: " + string.Join(", ", _engine.MidiOutputDevices.Select(d => d.Name));
    }
    private void ListPacketCodecs()
    {
        // The packet seam is what CodeBrix.VideoPlayback feeds for WebM / CodeBrixVideo sound tracks. It is
        // entirely managed: whatever Core and the registered add-ons carry is what Android has.
        _status.Text = "Packet codecs: " + string.Join(", ", SharedAudioOutput.SupportedPacketCodecIds)
            + "\nflac packets supported: " + SharedAudioOutput.IsPacketCodecSupported("flac")
            + " (needs a Core that carries FlacPacketCodecFactory)";
    }
    private void UpdateMetrics()
    {
        if (IsFinishing || IsDestroyed) return;
        try
        {
            var d = (_playback as IAndroidAudioDevice)?.GetDiagnostics();
            _metrics.Text = $"{AndroidBuild.SupportedAbis?.FirstOrDefault()} / API {(int)AndroidBuild.VERSION.SdkInt}\n"
                + $"Callbacks {d?.CallbackCount}, xruns {d?.XRunCount}, burst {d?.FramesPerBurst}, buffer {d?.BufferFrames}\n"
                + $"Max callback {d?.MaximumCallbackMicroseconds:F0} µs; callback allocation {d?.CallbackAllocatedBytes} bytes\n"
                + $"Capture: {Volatile.Read(ref _recordedCount) / 96000.0:F1} s; GC {GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}\n"
                + $"Native error {d?.NativeError}; {_engine.LastBackendError?.Message ?? d?.CallbackException?.Message}";
            if (Volatile.Read(ref _recordedCount) == _recorded.Length) _capture?.Stop();
        }
        catch (ObjectDisposedException) { }
    }
    private void StopAll()
    {
        _capture?.Stop(); _playback?.Stop(); _focus.Abandon();
        _status.Text = "Stopped. This diagnostics app intentionally pauses when it leaves the foreground.";
    }
    protected override void OnStop() { StopAll(); base.OnStop(); }
    protected override void OnDestroy()
    {
        _timer?.Dispose(); _engine.Dispose(); _voice?.Dispose(); _focus.Dispose();
        CodeBrix.Audio.Wave.SharedAudioOutput.Shutdown(); base.OnDestroy();
    }

    private sealed class SynthVoice : SoundComponent
    {
        private readonly ModestSynthesizer _synth;
        private readonly float[] _left = new float[1024];
        private readonly float[] _right = new float[1024];
        internal SynthVoice(AudioEngine engine, int voices) : base(engine, AudioFormat.DvdHq)
        {
            _synth = new ModestSynthesizer(ModestSynthPresets.SawLead(), new ModestSynthesizerSettings(48000)
            { MaximumPolyphony = voices, MasterVolume = 0.1f / MathF.Sqrt(voices) });
            for (int i = 0; i < voices; ++i) _synth.NoteOn(i % 16, 36 + i % 60, 80);
        }
        protected override void GenerateAudio(Span<float> buffer, int channels)
        {
            for (int offset = 0; offset < buffer.Length;)
            {
                int frames = Math.Min(_left.Length, (buffer.Length - offset) / channels);
                _synth.Render(_left.AsSpan(0, frames), _right.AsSpan(0, frames));
                for (int i = 0; i < frames; ++i) { buffer[offset++] = _left[i]; buffer[offset++] = _right[i]; }
            }
        }
    }
}
