# Embedded Bink playback (Windows x64)

This is the default UI playback backend. It reads the Firefly logo, intro, menu
background and curtain Bink files through FFmpeg libraries inside the Unity
process. Intros have streaming audio and click-to-skip; menu videos loop with
original alpha. Playback launches no executable and writes no converted video.
Sea, river and moat surfaces also decode their original Bink files through the
same backend. No external FFmpeg executable, converter, or video-cache manager
remains. Startup and map loading do not convert videos.

## Setup and use

Install Visual Studio's C++ desktop build tools, then run from the repository root:

```powershell
.\Tools\EmbeddedFFmpeg\Setup.ps1
```

The script downloads a checksum-pinned Windows x64 LGPL shared SDK, verifies its
SHA-256, builds the small native bridge with MSVC, and installs the required DLLs
with Windows x64-only Unity plugin metadata. The SDK/headers stay in ignored
`Temp/EmbeddedFFmpeg`; generated runtime DLLs stay in ignored
`Assets/Plugins/x86_64/EmbeddedFFmpeg`. No FFmpeg source tree is required in Assets.
After setup, let Unity import the plugins. Restart Unity before rebuilding any
DLL it has already loaded; native plugins cannot be replaced while loaded.

Enter Play mode in the normal scene; no inspector setting is required. Click to
skip the current intro. The previous opt-in checkbox and cached UI player have
been removed. Other native platforms are not yet supported. Re-run setup after
updating the bridge ABI, including the audio exports.

`-SdkDirectory <path>` accepts an already unpacked compatible SDK for offline
rebuilds. The caller is responsible for verifying that supplied SDK; the automatic
download path always checks the pinned archive hash.

## Code boundaries

- `Tools/EmbeddedFFmpeg/opensh2_bink.c`: C ABI; demux/decode, aligned RGBA conversion,
  EOF draining, rewind/decoder flush, stereo float PCM resampling, and native ownership.
- `Assets/Code/Video/EmbeddedFFmpeg`: C# interop and worker decoding, with no Unity
  API calls in decoding workers. Four reusable managed RGBA buffers and at most three queued frames
  bound memory independently of movie duration. Each native decoder also owns one
  aligned RGBA conversion frame and FFmpeg's internal decoding buffers. Audio has
  an independent worker/demuxer and a two-second, 48 kHz stereo float ring so video
  backpressure cannot starve audio. Native resampling supports the logo's 20,025 Hz
  source audio as well as the intro's 48 kHz audio. EOF drains decoder/resampler tails.
- `Assets/Code/Video/EmbeddedFFmpeg/BinkTexturePlayer.cs`: silent, looping Unity
  texture presentation driven by caller-supplied delta time. Terrain uses repeat
  wrapping and bilinear filtering; zero speed still displays the first frame.
- `Assets/Code/Stronghold2/TerrainRendering/S2MWaterVideoPlayer.cs`: binds sea,
  river and moat textures to water materials using original game paths. It
  preserves game-time pause and the configured playback multipliers. Disabling
  releases workers/textures; re-enabling restarts playback. Destruction also
  releases generated mesh/material resources through the renderer's resource owner.
- `Assets/Code/UI/Video/EmbeddedFFmpeg`: main-thread texture upload, monotonic
  streaming AudioClip/AudioSource ownership and UI Toolkit integration through
  `IVideoSurface`. Intros schedule audio on Unity's DSP clock and use that same
  clock for video; silent menu videos use an unscaled monotonic clock. PCM callbacks
  only copy buffered floats, fill underruns with silence, and never decode or allocate.
  Late audio samples are discarded after underruns to preserve the audio timeline.
- `Assets/Code/UI/Frontend`: intro sequencing and click-to-skip. The opening
  curtain stays displayed until the idle video's first frame is uploaded.

Source alpha is preserved as straight RGBA. Conversion uses an aligned native
frame before packing/flipping rows for Unity; this preserves the odd 381-pixel
curtain width without SIMD tail corruption. Readable point-filtered video
textures avoid the default UI Toolkit static atlas. Frame timestamps use Bink 1's
constant frame rate. Preparation is bounded to 30 seconds; disposal cancels queue
waits, stops the audio source, joins decoding workers, closes native handles, and
destroys Unity textures/clips. A small prebuffer and scheduled start prevent audio
from running ahead during video preparation. Natural completion waits for both
video and audio tails. Audio is currently forward-only for non-looping intros;
silent video loops are supported. This is a CPU decode/upload implementation.

## Dependency provenance and licensing

- FFmpeg version: `n8.1.3-29-gad3b795b72-20261101`.
- FFmpeg source: <https://github.com/FFmpeg/FFmpeg/tree/ad3b795b72> (unmodified).
- Build provider and recipes: <https://github.com/BtbN/FFmpeg-Builds>.
- Pinned build: <https://github.com/BtbN/FFmpeg-Builds/releases/tag/autobuild-2026-11-01-12-49>.
- Archive: `ffmpeg-n8.1.3-29-gad3b795b72-win64-lgpl-shared-8.1.zip`.
- SHA-256: `786b43f77ea260af71fbb1bcdaabd43df83492122fb5575027f659d490d30b37`.
- This particular build uses **LGPL v3** (`--enable-version3`, without
  `--enable-gpl`); it is dynamically linked. Setup copies its license beside the
  runtime DLLs. Only dynamically linked runtime libraries are needed by the game.

The generated third-party binaries are not committed. A player distribution that
includes them needs the applicable notices, corresponding FFmpeg/dependency source
and build materials, and the ability to replace the dynamically linked libraries.
These setup files are not a complete third-party release compliance
bundle. See <https://ffmpeg.org/legal.html> before packaging a release.

## Verification

```powershell
python Tools/EmbeddedFFmpeg/ValidateNative.py
python Tools/EmbeddedFFmpeg/ValidateAudio.py
```

The native check decodes all six menu/terrain Bink files twice, checking full-frame hashes
after rewinding, frame counts/timing, odd-width preservation and exact curtain
alpha against independently extracted source alpha. Its independent test oracle
is the pinned SDK's executable under `Temp`, or a path supplied with `--ffmpeg`.
It does not depend on the removed `Assets/External/ffmpeg-windows` folder.
The audio check compares all output samples of both intros against a separate
FFmpeg CLI decode (including sample-rate conversion and trailing audio). It uses
the pinned SDK's test executable; that executable is not part of runtime playback.

`Notes/tools/EmbeddedBinkUnityValidation.cs` is a batch Play mode integration
check for a scratch project containing the same sources/plugins and packages.
Copy it to that project's `Assets/Editor` and run Unity with
`-batchmode -executeMethod EmbeddedBinkUnityValidation.Run` (without `-quit`).
It exercises default startup, two displayed loops of each menu video, curtain
handoff, changing GPU video pixels, GPU alpha compositing over a known color,
absence of menu VideoPlayers, and worker/texture disposal. Artifacts are written
to `Temp/EmbeddedBinkValidation` in the main checkout.

`Notes/tools/FrontendAudioUnityValidation.cs` runs with no video cache or converter
executable. It verifies actual PCM at Unity's AudioSource output, natural logo
completion, DSP/video synchronization with timeScale zero, intro click-to-skip,
early skip/disable during preparation, worker/source cleanup, menu loops and alpha,
and that no conversion task or cache directory is created. Use
`-batchmode -executeMethod FrontendAudioUnityValidation.Run` without `-quit`.
`FrontendAudioUnityValidation.RunNaturalIntro` additionally plays the full
124.96-second intro, checks synchronization throughout, and verifies that every
decoded PCM frame reached Unity before natural completion.
The layout smoke check remains `Notes/tools/FrontendUnityValidation.cs`.

`Notes/tools/TerrainBinkUnityValidation.cs` exercises the real S2M water renderer
with a real installed map and no video cache/external executable. It verifies two
displayed loops of sea, river and moat, changing GPU texture pixels and material
bindings, configured speed, zero-speed frame holding, game-time pause/half-speed,
disable/re-enable, and worker/texture/material cleanup. Copy it into the scratch
project's `Assets/Editor` and use
`-batchmode -executeMethod TerrainBinkUnityValidation.Run` without `-quit`.
GPU snapshots and loop diagnostics go to `Temp/TerrainBinkValidation`.
