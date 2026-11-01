# OpenSH2 UI

All frontend presentation lives in this directory under `Assets.Code.UI` namespaces.
No Canvas hierarchy or UI scene setup is required: `Main.Start()` attaches
`Frontend.FrontendController`, which creates the runtime UIDocument and panel.

- `Frontend/`: startup flow, menu binding, original-game artwork loading and paths.
- `Video/`: reusable UI Toolkit video surface interface.
- `Video/EmbeddedFFmpeg/`: direct Bink surfaces and streaming Unity audio; the non-UI decoder is
  in `Assets/Code/Video/EmbeddedFFmpeg`, and native build/setup lives in
  `Tools/EmbeddedFFmpeg`. See that directory's README for setup and verification.
- `Resources/OpenSH2/Frontend/`: UXML screen hierarchy and USS styling, loaded through Resources in editor and builds.

Startup plays the original Firefly logo and intro Bink files with audio, then
shows the main menu. No UI conversion or video cache is required. A left pointer
press skips the current intro only and stops its audio. Missing or failed videos
log a warning and let startup continue. Preparation and first-frame waits time out
after 30 seconds. The native FFmpeg runtime must be installed (currently Windows x64).

The menu loops the background while the curtain opens, then switches to a
preprepared curtain idle loop after its first frame is ready. Original Bink alpha
is preserved without a WebM intermediary. Video, audio and texture resources are
released when the frontend is disabled.

Embedded FFmpeg is the UI's default backend; no inspector opt-in is needed. Audio
uses a bounded PCM ring and Unity streaming AudioClip, with video synchronized to
the audio DSP clock. Silent menu loops use an unscaled monotonic clock. The
same embedded decoder also drives terrain water textures through
`S2MWaterVideoPlayer`. The external converter and video-cache manager have been
removed. Old converted video files are unused and are left untouched.

Edit `MainMenu.uxml` for controls and `MainMenu.uss` for their positions and styles.
The menu uses a 1024 x 768 reference frame fitted uniformly into the viewport;
wide viewports get original stone sidebar artwork. Intro videos fit their own
aspect ratio against black. Proprietary artwork is loaded from the installed
game, never embedded in these layout files.

Exit quits a player build or stops Play mode in the editor. Other menu actions
are disabled placeholders pending their screen implementations. Automatic test
map loading is disabled. Future map navigation should hide/dispose the frontend,
load the selected map, then enter `GameState.PLAYING` to advance simulation.
