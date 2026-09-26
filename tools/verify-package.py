"""Check the packed Android native AAR and the platform-neutral Opus dependency."""
from pathlib import Path
import hashlib
import io
import sys
import xml.etree.ElementTree as ET
import zipfile

repo = Path(__file__).resolve().parents[1]
feed, version = Path(sys.argv[1]), sys.argv[2]
for name in ('CodeBrix.Audio.Android.ApacheLicenseForever', 'CodeBrix.Audio.Opus.BsdLicenseForever'):
    with zipfile.ZipFile(feed / f'{name}.{version}.nupkg') as package:
        spec = ET.fromstring(package.read(name + '.nuspec'))
        dependencies = [d.attrib for d in spec.iter() if d.tag.endswith('}dependency')]
        assert len(dependencies) == 1, dependencies
        assert dependencies[0]['id'] == 'CodeBrix.Audio.Core.MitLicenseForever', dependencies
        assert dependencies[0]['version'] == version, dependencies
        if name.startswith('CodeBrix.Audio.Android.'):
            aar_names = [n for n in package.namelist() if n.endswith('.aar')]
            assert len(aar_names) == 1, aar_names
            with zipfile.ZipFile(io.BytesIO(package.read(aar_names[0]))) as aar:
                libraries = [n for n in aar.namelist() if n.endswith('.so')]
                assert set(libraries) == {f'jni/{abi}/libcodebrix_miniaudio.so' for abi in ('arm64-v8a', 'x86_64')}, libraries
                for abi in ('arm64-v8a', 'x86_64'):
                    packed = aar.read(f'jni/{abi}/libcodebrix_miniaudio.so')
                    built = (repo / f'src/CodeBrix.Audio.Android/native/{abi}/libcodebrix_miniaudio.so').read_bytes()
                    assert packed == built, abi
                    print(f'{abi} packed binary: {hashlib.sha256(packed).hexdigest()}')
            assert 'AGENT-README.txt' in package.namelist()
            assert 'THIRD-PARTY-NOTICES.txt' in package.namelist()
        print(f'{name}: Core dependency only, version {version}.')
