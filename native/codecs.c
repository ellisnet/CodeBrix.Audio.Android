/* CodeBrix codec-only ABI. MIT miniaudio and stb_vorbis retain their own licenses.
 * The sf_* names preserve the CodeBrix.Audio.Engine native codec ABI. */
#define MA_NO_DEVICE_IO
#define MA_NO_ENGINE
#define MA_NO_RESOURCE_MANAGER
#define MA_NO_NODE_GRAPH
#define MA_NO_GENERATION
#define STB_VORBIS_HEADER_ONLY
#include "vendor/stb_vorbis-31c1ad3/stb_vorbis.c"
#define MINIAUDIO_IMPLEMENTATION
#include "vendor/miniaudio-80cf7b2/miniaudio.h"

MA_API void sf_free(void *p) { ma_free(p, NULL); }
MA_API ma_decoder *sf_allocate_decoder(void) { return ma_malloc(sizeof(ma_decoder), NULL); }
MA_API ma_encoder *sf_allocate_encoder(void) { return ma_malloc(sizeof(ma_encoder), NULL); }
MA_API ma_decoder_config *sf_allocate_decoder_config(ma_format f, ma_uint32 c, ma_uint32 r) {
    ma_decoder_config *p = ma_malloc(sizeof(*p), NULL);
    if (p) *p = ma_decoder_config_init(f, c, r);
    return p;
}
MA_API ma_encoder_config *sf_allocate_encoder_config(ma_format f, ma_uint32 c, ma_uint32 r) {
    ma_encoder_config *p = ma_malloc(sizeof(*p), NULL);
    if (p) *p = ma_encoder_config_init(ma_encoding_format_wav, f, c, r);
    return p;
}
MA_API int sf_has_vorbis(void) {
#ifdef MA_HAS_VORBIS
    return 1;
#else
    return 0;
#endif
}
#undef STB_VORBIS_HEADER_ONLY
#include "vendor/stb_vorbis-31c1ad3/stb_vorbis.c"
