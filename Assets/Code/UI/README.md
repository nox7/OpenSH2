# OpenSH2 UI

All frontend presentation lives in this directory under `Assets.Code.UI` namespaces.
No Canvas hierarchy or UI scene setup is required: `Main.Start()` attaches
`Frontend.FrontendController`, which creates the runtime UIDocument and panel.

- `Frontend/`: startup flow, menu binding, original-game artwork loading and paths.
- `Video/`: reusable UI Toolkit video surfaces and their native player/render-target lifetime.
- `Video/EmbeddedFFmpeg/`: opt-in direct Bink menu surfaces; the non-UI decoder is
  in `Assets/Code/Video/EmbeddedFFmpeg`, and native build/setup lives in
  `Tools/EmbeddedFFmpeg`. See that directory's README for setup and verification.
- `Resources/OpenSH2/Frontend/`: UXML screen hierarchy and USS styling, loaded through Resources in editor and builds.

Startup caches videos through the existing CacheManager, plays the Firefly logo
and intro, then shows the main menu. A left pointer press skips the current intro
only. Missing or failed videos log a warning and let startup continue. Video
preparation and first-frame waits time out after 30 seconds.

The menu loops the background while the curtain opens, then switches to a
preprepared curtain idle loop after its first frame is ready. The transparent
WebM curtain cache is retained. Video and texture resources are released when
the frontend is disabled.

`Main.Use Embedded Menu Bink` selects the Windows x64 embedded FFmpeg prototype
for the three menu videos only. It is off by default; intros and cache preparation
retain their existing behavior. Both backends use the same UXML/USS layout.

Edit `MainMenu.uxml` for controls and `MainMenu.uss` for their positions and styles.
The menu uses a 1024 x 768 reference frame fitted uniformly into the viewport;
wide viewports get original stone sidebar artwork. Intro videos fit their own
aspect ratio against black. Proprietary artwork is loaded from the installed
game, never embedded in these layout files.

Exit quits a player build or stops Play mode in the editor. Other menu actions
are disabled placeholders pending their screen implementations. Automatic test
map loading is disabled. Future map navigation should hide/dispose the frontend,
load the selected map, then enter `GameState.PLAYING` to advance simulation.
