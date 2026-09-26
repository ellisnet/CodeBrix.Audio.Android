// Copyright (c) 2026 Jeremy Ellis and contributors. SPDX-License-Identifier: Apache-2.0
#include <oboe/Oboe.h>
#include <atomic>
#include <cstring>
#include <memory>
#include <new>

#define CB_API extern "C" __attribute__((visibility("default")))
using Callback = int32_t (*)(void *, float *, int32_t);

// The error callback is owned by Oboe as well as the holder. It never accesses managed
// state: Oboe may finish an error notification on its worker after close returns.
class Callbacks final : public oboe::AudioStreamDataCallback, public oboe::AudioStreamErrorCallback {
public:
    Callback callback;
    void *user;
    int32_t channels;
    bool input;
    std::atomic<int32_t> error{0};
    std::atomic<int64_t> count{0};
    oboe::DataCallbackResult onAudioReady(oboe::AudioStream *, void *data, int32_t frames) override {
        if (!input) std::memset(data, 0, static_cast<size_t>(frames) * channels * sizeof(float));
        count.fetch_add(1, std::memory_order_relaxed);
        return callback(user, static_cast<float *>(data), frames) == 0
            ? oboe::DataCallbackResult::Continue : oboe::DataCallbackResult::Stop;
    }
    bool onError(oboe::AudioStream *, oboe::Result result) override {
        error.store(static_cast<int32_t>(result), std::memory_order_release);
        // The control thread observes the error and owns stop/close/reopen. Returning true
        // prevents Oboe's automatic close from racing the application's control thread.
        return true;
    }
};
struct Stream {
    std::shared_ptr<Callbacks> callbacks;
    std::shared_ptr<oboe::AudioStream> audio;
};

CB_API int32_t cb_oboe_abi_version() { return 1; }
CB_API int32_t cb_oboe_open(int32_t input, int32_t device, int32_t rate, int32_t channels,
                          int32_t exclusive, int32_t burstCount, int32_t usage, int32_t preset,
                          Callback callback, void *user, Stream **result) {
    if (!result || !callback || rate < 8000 || rate > 384000 || channels < 1 || channels > 8
        || burstCount < 1 || burstCount > 16) return static_cast<int32_t>(oboe::Result::ErrorIllegalArgument);
    *result = nullptr;
    try {
        auto holder = std::make_unique<Stream>();
        holder->callbacks = std::make_shared<Callbacks>();
        auto &state = *holder->callbacks;
        state.callback = callback; state.user = user; state.channels = channels; state.input = input != 0;
        oboe::AudioStreamBuilder builder;
        builder.setAudioApi(oboe::AudioApi::AAudio)
            ->setDirection(input ? oboe::Direction::Input : oboe::Direction::Output)
            ->setDeviceId(device)->setSampleRate(rate)->setChannelCount(channels)
            ->setFormat(oboe::AudioFormat::Float)->setFormatConversionAllowed(true)
            ->setChannelConversionAllowed(true)->setSampleRateConversionQuality(oboe::SampleRateConversionQuality::Medium)
            ->setPerformanceMode(oboe::PerformanceMode::LowLatency)
            ->setSharingMode(exclusive ? oboe::SharingMode::Exclusive : oboe::SharingMode::Shared)
            ->setUsage(static_cast<oboe::Usage>(usage))->setInputPreset(static_cast<oboe::InputPreset>(preset))
            ->setDataCallback(holder->callbacks)->setErrorCallback(holder->callbacks);
        auto opened = builder.openStream(holder->audio);
        if (opened != oboe::Result::OK) return static_cast<int32_t>(opened);
        auto &audio = *holder->audio;
        if (audio.getFormat() != oboe::AudioFormat::Float || audio.getChannelCount() != channels
            || audio.getSampleRate() != rate) {
            audio.close();
            return static_cast<int32_t>(oboe::Result::ErrorInvalidFormat);
        }
        audio.setBufferSizeInFrames(audio.getFramesPerBurst() * burstCount);
        *result = holder.release();
        return 0;
    } catch (...) { return static_cast<int32_t>(oboe::Result::ErrorNoMemory); }
}
CB_API int32_t cb_oboe_start(Stream *s) { return static_cast<int32_t>(s->audio->start()); }
CB_API int32_t cb_oboe_stop(Stream *s) { return static_cast<int32_t>(s->audio->stop()); }
CB_API void cb_oboe_close(Stream *s) {
    if (!s) return;
    s->audio->stop();
    s->audio->close(); // No more audio callbacks after close; error callback owns no managed data.
    delete s;
}
CB_API int32_t cb_oboe_error(Stream *s) { return s->callbacks->error.load(std::memory_order_acquire); }
CB_API int64_t cb_oboe_callbacks(Stream *s) { return s->callbacks->count.load(std::memory_order_relaxed); }
CB_API int32_t cb_oboe_xruns(Stream *s) {
    auto value = s->audio->getXRunCount();
    return value ? value.value() : -1;
}
CB_API int32_t cb_oboe_burst(Stream *s) { return s->audio->getFramesPerBurst(); }
CB_API int32_t cb_oboe_buffer(Stream *s) { return s->audio->getBufferSizeInFrames(); }
CB_API int32_t cb_oboe_device(Stream *s) { return s->audio->getDeviceId(); }
