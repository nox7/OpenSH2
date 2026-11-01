"""Compare complete intro PCM to an independent FFmpeg CLI decode, including resampler drain."""
import array
import ctypes as C
import hashlib
import json
import math
import os
from pathlib import Path
import subprocess

repo=Path(__file__).resolve().parents[2]
runtime=repo/'Assets/Plugins/x86_64/EmbeddedFFmpeg'
search=os.add_dll_directory(str(runtime))
lib=C.CDLL(str(runtime/'OpenSH2.Bink.dll'))
lib.osh2_bink_audio_open.argtypes=[C.c_char_p,C.c_char_p,C.c_int]
lib.osh2_bink_audio_open.restype=C.c_void_p
lib.osh2_bink_audio_read.argtypes=[C.c_void_p,C.c_void_p,C.c_int,C.c_char_p,C.c_int]
lib.osh2_bink_audio_read.restype=C.c_int
lib.osh2_bink_audio_close.argtypes=[C.c_void_p]
ffmpeg=next((repo/'Temp/EmbeddedFFmpeg/pinned-sdk').glob('*/bin/ffmpeg.exe'))
error=C.create_string_buffer(1024)
report={}
for name in ['firefly_logo.bik','Stronghold2 FMV.bik']:
    path=Path('C:/Steam/steamapps/common/Stronghold 2/ui/frontend')/name
    handle=lib.osh2_bink_audio_open(str(path).encode(),error,len(error))
    assert handle,error.value
    pcm=bytearray()
    try:
        buffer=(C.c_float*(4096*2))()
        while True:
            count=lib.osh2_bink_audio_read(handle,buffer,4096,error,len(error))
            assert 0<=count<=4096,error.value
            if count==0:break
            pcm.extend(C.string_at(buffer,count*2*4))
    finally:lib.osh2_bink_audio_close(handle)
    oracle=subprocess.run([str(ffmpeg),'-v','error','-i',str(path),'-map','0:a:0',
        '-ar','48000','-ac','2','-c:a','pcm_f32le','-f','f32le','pipe:1'],
        stdout=subprocess.PIPE,check=True).stdout
    assert len(pcm)==len(oracle),(name,len(pcm),len(oracle))
    values=array.array('f');values.frombytes(pcm)
    assert all(math.isfinite(x) for x in values)
    peak=max(abs(x) for x in values)
    assert peak>.01,'Decoded audio is silent'
    exact=pcm==oracle
    if not exact:
        reference=array.array('f');reference.frombytes(oracle)
        maximum=max(abs(a-b) for a,b in zip(values,reference))
        assert maximum<1e-5,maximum
    report[name]=dict(frames=len(pcm)//8,seconds=len(pcm)/8/48000,peak=peak,
        exact_cli_match=exact,sha256=hashlib.sha256(pcm).hexdigest())
# Silent menu videos produce an explicit no-audio result, not an error.
path=Path('C:/Steam/steamapps/common/Stronghold 2/ui/frontend/background.bik')
assert not lib.osh2_bink_audio_open(str(path).encode(),error,len(error)) and not error.value
assert not lib.osh2_bink_audio_open(b'no-such-audio.bik',error,len(error)) and error.value
print(json.dumps(report,indent=2))
