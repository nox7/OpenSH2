# Embedded Bink playback prototype (Windows x64)

This is an opt-in, silent-menu prototype. It reads `background.bik`,
`curtain_animation.bik` and `curtain_idle.bik` directly through FFmpeg libraries
inside the Unity process. Playback launches no executable and writes no converted
video. Intro playback, the conversion cache, and the default menu backend remain
unchanged. Audio-bearing files are explicitly rejected rather than played silently.

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

On the scene's **Main** component enable **Use Embedded Menu Bink**, then enter
Play mode and skip the intros. Turn the setting off to return to cached playback.
The existing cache preparation still runs at startup: this prototype has not
replaced or bypassed the conversion system. Other platforms should leave it off.

`-SdkDirectory <path>` accepts an already unpacked compatible SDK for offline
rebuilds. The caller is responsible for verifying that supplied SDK; the automatic
download path always checks the pinned archive hash.

## Code boundaries

- `Tools/EmbeddedFFmpeg/opensh2_bink.c`: C ABI; demux/decode, aligned RGBA conversion,
  EOF draining, rewind/decoder flush, and native resource ownership.
- `Assets/Code/Video/EmbeddedFFmpeg`: C# interop and worker decoding, with no Unity
  API calls. Four reusable managed RGBA buffers and at most three queued frames
  bound memory independently of movie duration. Each native decoder also owns one
  aligned RGBA conversion frame and FFmpeg's internal decoding buffers.
- `Assets/Code/UI/Video/EmbeddedFFmpeg`: main-thread texture upload, monotonic
  unscaled playback clock, frame dropping when presentation falls behind, and
  UI Toolkit integration through `IVideoSurface`.
- `Assets/Code/UI/Frontend`: selects the backend for menu surfaces. The opening
  curtain stays displayed until the idle video's first frame is uploaded.

Source alpha is preserved as straight RGBA. Conversion uses an aligned native
frame before packing/flipping rows for Unity; this preserves the odd 381-pixel
curtain width without SIMD tail corruption. Readable point-filtered video
textures avoid the default UI Toolkit static atlas. Frame timestamps use Bink 1's
constant frame rate. Preparation is bounded to 30 seconds; disposal cancels queue
waits, joins the worker, closes native handles, and destroys the Unity texture.
This is a CPU decode/upload implementation, not a zero-copy GPU integration.

## Dependency provenance and licensing

- FFmpeg version: `n8.1.3-29-gad3b795b72-20261101`.
- FFmpeg source: <https://github.com/FFmpeg/FFmpeg/tree/ad3b795b72> (unmodified).
- Build provider and recipes: <https://github.com/BtbN/FFmpeg-Builds>.
- Pinned build: <https://github.com/BtbN/FFmpeg-Builds/releases/tag/autobuild-2026-11-01-12-49>.
- Archive: `ffmpeg-n8.1.3-29-gad3b795b72-win64-lgpl-shared-8.1.zip`.
- SHA-256: `786b43f77ea260af71fbb1bcdaabd43df83492122fb5575027f659d490d30b37`.
- This particular build uses **LGPL v3** (`--enable-version3`, without
  `--enable-gpl`); it is dynamically linked. Setup copies its license beside the
  runtime DLLs. The pre-existing converter executable is a separate dependency.

The generated third-party binaries are not committed. A player distribution that
includes them needs the applicable notices, corresponding FFmpeg/dependency source
and build materials, and the ability to replace the dynamically linked libraries.
These prototype setup files are not a complete third-party release compliance
bundle. See <https://ffmpeg.org/legal.html> before packaging a release.

## Verification

```powershell
python Tools/EmbeddedFFmpeg/ValidateNative.py
```

This decodes all three installed Bink files twice, checking full-frame hashes
after rewinding, frame counts/timing, odd-width preservation and exact curtain
alpha against independently extracted source alpha. The converter executable is
used only as a test oracle, not by the embedded playback backend.

`Notes/tools/EmbeddedBinkUnityValidation.cs` is a batch Play mode integration
check for a scratch project containing the same sources/plugins, packages, and
intro caches. Copy it to that project's `Assets/Editor` and run Unity with
`-batchmode -executeMethod EmbeddedBinkUnityValidation.Run` (without `-quit`).
It exercises opt-in startup, two displayed loops of each menu video, curtain
handoff, changing GPU video pixels, GPU alpha compositing over a known color,
absence of menu VideoPlayers, and worker/texture disposal. Artifacts are written
to `Temp/EmbeddedBinkValidation` in the main checkout. The normal cached-backend
smoke check remains `Notes/tools/FrontendUnityValidation.cs`.
