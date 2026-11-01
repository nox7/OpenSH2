"""Real-file ABI, alpha and rewind regression. CLI FFmpeg is used only as a test oracle."""
import argparse
import ctypes as C
import hashlib
import json
import os
from pathlib import Path
import subprocess

repo = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument('--game', type=Path, default=Path('C:/Steam/steamapps/common/Stronghold 2'))
parser.add_argument('--ffmpeg', type=Path, help='Independent test oracle; defaults to the pinned SDK executable in Temp.')
args = parser.parse_args()
ffmpeg = args.ffmpeg or next((repo/'Temp/EmbeddedFFmpeg/pinned-sdk').glob('*/bin/ffmpeg.exe'), None)
if ffmpeg is None: raise SystemExit('Run Tools/EmbeddedFFmpeg/Setup.ps1 first, or pass --ffmpeg.')
runtime = repo / 'Assets/Plugins/x86_64/EmbeddedFFmpeg'
search = os.add_dll_directory(str(runtime))
lib = C.CDLL(str(runtime / 'OpenSH2.Bink.dll'))
lib.osh2_bink_open.argtypes = [C.c_char_p,C.POINTER(C.c_int),C.POINTER(C.c_int),C.POINTER(C.c_double),C.c_char_p,C.c_int]
lib.osh2_bink_open.restype = C.c_void_p
lib.osh2_bink_read.argtypes = [C.c_void_p,C.c_void_p,C.c_int,C.POINTER(C.c_double),C.c_char_p,C.c_int]
lib.osh2_bink_read.restype = C.c_int
lib.osh2_bink_rewind.argtypes = [C.c_void_p,C.c_char_p,C.c_int]
lib.osh2_bink_rewind.restype = C.c_int
lib.osh2_bink_close.argtypes = [C.c_void_p]
error = C.create_string_buffer(1024)
report = {}
for relative,dimensions,count in [('ui/frontend/background.bik',(736,768),241),('ui/frontend/curtain_animation.bik',(381,768),136),('ui/frontend/curtain_idle.bik',(381,768),96),('terrain/river/sea.bik',(256,256),125),('terrain/river/river_full_.bik',(256,256),84),('terrain/river/moat.bik',(256,256),125)]:
    path = args.game / relative
    name = path.name
    w,h,step = C.c_int(),C.c_int(),C.c_double()
    handle = lib.osh2_bink_open(str(path).encode(),C.byref(w),C.byref(h),C.byref(step),error,len(error))
    assert handle, error.value
    try:
        assert (w.value,h.value) == dimensions, (w.value,h.value)
        pixels = (C.c_ubyte * (w.value*h.value*4))()
        passes = []
        alpha_first = None
        for iteration in range(2):
            hashes = []
            while True:
                pts = C.c_double()
                result = lib.osh2_bink_read(handle,pixels,len(pixels),C.byref(pts),error,len(error))
                assert result >= 0,error.value
                if result == 0: break
                assert abs(pts.value-len(hashes)*step.value) < 1e-8
                data = bytes(pixels)
                if not hashes:
                    alpha_first = data[3::4]
                    # Independent RGBA conversion supplies alpha even for opaque sources.
                    oracle = subprocess.run([str(ffmpeg),'-v','error','-i',str(path),
                        '-vf','format=rgba,vflip','-frames:v','1','-pix_fmt','rgba','-f','rawvideo','pipe:1'],
                        check=True,stdout=subprocess.PIPE).stdout
                    assert alpha_first == oracle[3::4], 'Source alpha plane or row orientation was changed.'
                    if name.startswith('curtain'):
                        assert min(alpha_first)==0 and max(alpha_first)==255
                hashes.append(hashlib.sha256(data).hexdigest())
            passes.append(hashes)
            assert hashes, 'No frames decoded'
            if iteration==0: assert lib.osh2_bink_rewind(handle,error,len(error))==0,error.value
        assert passes[0]==passes[1], 'Frames differ after rewind (decoder history was not reset).'
        assert len(passes[0])==count, (name,len(passes[0]),'expected',count)
        report[name] = dict(width=w.value,height=h.value,frames=len(passes[0]),fps=1/step.value,
            passes=2,alpha_min=min(alpha_first),alpha_max=max(alpha_first),
            partial_alpha_pixels=sum(0<a<255 for a in alpha_first),first_frame_sha256=passes[0][0])
    finally: lib.osh2_bink_close(handle)
w,h,step=C.c_int(),C.c_int(),C.c_double()
assert not lib.osh2_bink_open(b'no-such-video.bik',C.byref(w),C.byref(h),C.byref(step),error,len(error))
print(json.dumps(report,indent=2))
