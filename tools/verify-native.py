"""Verify the exact native files packaged, including codec ABI and 16 KB ELF alignment."""
from pathlib import Path
import hashlib
import re
import subprocess
import sys

repo = Path(__file__).resolve().parents[1]
ndk = Path(sys.argv[1])
llvm = ndk / 'toolchains/llvm/prebuilt/linux-x86_64/bin'
ndk_version = re.search(r'^Pkg.Revision\s*=\s*(.+)$', (ndk / 'source.properties').read_text(), re.M).group(1)
required = '''sf_free sf_allocate_decoder sf_allocate_encoder sf_allocate_decoder_config
sf_allocate_encoder_config sf_has_vorbis ma_decoder_init ma_decoder_init_memory
ma_decoder_uninit ma_decoder_read_pcm_frames ma_decoder_seek_to_pcm_frame
ma_decoder_get_length_in_pcm_frames ma_encoder_init ma_encoder_uninit
ma_encoder_write_pcm_frames cb_oboe_abi_version cb_oboe_open cb_oboe_start cb_oboe_stop
cb_oboe_close cb_oboe_error cb_oboe_callbacks cb_oboe_xruns cb_oboe_burst cb_oboe_buffer
cb_oboe_device'''.split()
report = []
for abi, machine in [('arm64-v8a', 'AArch64'), ('x86_64', 'Advanced Micro Devices X86-64')]:
    path = repo / 'src/CodeBrix.Audio.Android/native' / abi / 'libcodebrix_miniaudio.so'
    def read(*args):
        return subprocess.check_output([str(llvm/'llvm-readelf'), *args, str(path)], text=True)
    exports = read('--dyn-syms')
    for symbol in required:
        assert re.search(r'\b' + symbol + r'\s*$', exports, re.M), (abi, symbol)
    assert 'ma_device_init' not in exports, 'Miniaudio device IO must be disabled'
    assert machine in read('-h'), abi
    loads = [line for line in read('-lW').splitlines() if line.strip().startswith('LOAD ')]
    assert loads and all(int(line.split()[-1], 16) >= 16384 for line in loads), loads
    deps = read('-d')
    needed = re.findall(r'NEEDED.*\[(.*?)\]', deps)
    assert set(needed) <= {'liblog.so','libandroid.so','libOpenSLES.so','libm.so','libdl.so','libc.so'}, needed
    report.append(f'{abi}: {hashlib.sha256(path.read_bytes()).hexdigest()}\n  16 KB LOAD alignment; all {len(required)} ABI exports; dependencies: {needed}\n')
output = ''.join(report)
(repo/'native/BUILD-PROVENANCE.txt').write_text(f'NDK {ndk_version}; API 33; Release; C++ static runtime\n' + output)
print(output)
