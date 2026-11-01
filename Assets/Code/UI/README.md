# OpenSH2 UI

All frontend presentation lives in this directory under `Assets.Code.UI` namespaces.
No Canvas hierarchy or UI scene setup is required: `Main.Start()` attaches
`Frontend.FrontendController`, which creates the runtime UIDocument and panel.

- `Frontend/`: startup flow, UIDocument ownership and screen registration.
- `Assets/`: shared original-game artwork loading and asset paths.
- `Screens/`: page controllers under their own namespaces/directories (`MainMenu/`,
  `WarPeace/`, `PathOfPeaceSelection/`, `PathOfWarSelection/`, `CampaignSelection/`), plus the frontend screen interface/router.
- `Screens/Shared/`: the shared reference viewport, stone borders and context
  descriptions/previews. Shared artwork-button styling is in the resource
  `Screens/Shared/ScreenControls.uss`.
- `Video/`: reusable UI Toolkit video surface interface.
- `Video/EmbeddedFFmpeg/`: direct Bink surfaces and streaming Unity audio; the non-UI decoder is
  in `Assets/Code/Video/EmbeddedFFmpeg`, and native build/setup lives in
  `Tools/EmbeddedFFmpeg`. See that directory's README for setup and verification.
- `Resources/OpenSH2/Frontend/`: the startup shell and shared styling.
- `Resources/OpenSH2/Frontend/Screens/<Screen>/`: each page's UXML and USS, loaded
  through Resources in editor and builds.

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

Edit each page's UXML for controls and its USS for their positions and styles.
Pages share a 1024 x 768 reference frame fitted uniformly into the viewport;
wide viewports get original stone sidebar artwork. Intro videos fit their own
aspect ratio against black. Proprietary artwork is loaded from the installed
game, never embedded in these layout files.

The router creates each page on first use and reuses it on later visits. Register
new page factories in `FrontendController.Begin` and add their IDs to
`FrontendScreenId`. `Show`/`Hide` control visibility and transient playback; `Dispose`
removes callbacks and releases owned assets. Hidden title screens stop and release
their video workers; returning to the title starts the curtain opening again.
Ordinary frontend navigation stays in `GameState.MAIN_MENU`, so it does not advance
the simulation or load a map.

Play opens the war/peace selection screen. Its lower-left arrow returns to the
title, and Path of Peace opens `PathOfPeaceSelection`. That page shows Sim Campaign,
Free Build and Custom Maps with original green highlights, contextual images and
descriptions; its Back arrow returns to the path chooser. The default peace state
has no preview or description, and Back shows `Previous menu` without a preview.
Path of War opens `PathOfWarSelection`, with Campaign, Kingmaker, Siege, Conquest
Trail and Custom. It uses the same blank default state, hover/focus previews and
Back navigation as Peace. Tutorial and onward game-mode flows can be connected
when their screens are defined.
Hover descriptions take precedence over keyboard focus and return to the focused
control (or `Choose Your Path`) on pointer exit. Original off/on JPEGs keep the
choice scenes aligned rather than changing their crop on hover. The help label
uses an installed serif font, without copying a font into the project.

Exit quits a player build or stops Play mode in the editor. Multiplayer, map
editor, options and load remain disabled until their pages are implemented. Automatic test
map loading is disabled. Future map navigation should hide/dispose the frontend,
load the selected map, then enter `GameState.PLAYING` to advance simulation.

`Notes/tools/FrontendScreensUnityValidation.cs` checks Play/Back navigation,
screen reuse, all descriptions/highlight artwork, focus/hover precedence, shared
borders, multiple aspect ratios, and hidden title video cleanup. Run in a scratch
Unity project using `-batchmode -executeMethod FrontendScreensUnityValidation.Run`
without `-quit`. Rendered references are saved to `Temp/FrontendScreensValidation`.

`Notes/tools/PathOfPeaceSelectionUnityValidation.cs` checks peace-page navigation,
all three preview/help/highlight states, the blank default state, pointer/focus
precedence, Back/revisits, viewport fitting and resource cleanup. Run with
`-batchmode -executeMethod PathOfPeaceSelectionUnityValidation.Run` in the scratch
project without `-quit`; screenshots go to `Temp/PathOfPeaceSelectionValidation`.

`Notes/tools/PathOfWarSelectionUnityValidation.cs` checks war-page navigation,
all five preview/help/highlight states, blank default state, pointer/focus
precedence, Back/revisits, viewport fitting and resource cleanup. Run with
`-batchmode -executeMethod PathOfWarSelectionUnityValidation.Run` in the scratch
project without `-quit`; screenshots go to `Temp/PathOfWarSelectionValidation`.

Campaign opens `CampaignSelection`, with twelve original chapter map backgrounds,
chapter titles, faction symbols, selected text colors, and BMP/TGA button states.
Activation selects a chapter; focus/hover highlights its control without changing
persisted progress. Back returns to Path of War. Selection survives page revisits.
The forward arrow is visible but disabled until mission loading is connected;
its optional launch callback records an attempt only when actually launching.
Hovering the forward arrow temporarily shows `Play Mission X` for the selected
chapter, including while launch is disabled; pointer exit restores the chapter title.
Chapter 8 uses the original English game text, `Hawks Nest`.

Per-user settings/progress live outside UI in `Assets/Code/UserData`. The versioned
JSON profile is `%LOCALAPPDATA%/OpenSH2/profile.json` on Windows, independent of
Unity project identity and game save files. `UserProfileStore` supports player name,
war chapter completion/last attempt, and conquest trial completion IDs. Call
`Save` after settings changes, `RecordWarChapterAttempt` at actual mission startup,
and `CompleteWarChapter` on victory. Writes replace the file atomically and keep
a `.bak`; reads fall back to that backup if the main file cannot be read.

`CampaignProgress.CompleteAllForLocalTesting` defaults to true as requested for
current local builds. This is an in-memory presentation override: it never writes
fake completion to the profile. Set it false to enable actual progress filtering:
completed chapters and the last attempted chapter appear; chapter 1 is available
to a new player. Other chapter controls (including text/symbols) are hidden.
The last attempted chapter uses `current`, including when it was completed;
other completed chapters use `complete`. Selection does not update last attempted.

`Notes/tools/CampaignSelectionUnityValidation.cs` checks twelve chapter states,
partial progress and icons, persistence/backup recovery, testing-override isolation,
route/back/reuse, three viewport shapes, and resource cleanup. Use
`-batchmode -executeMethod CampaignSelectionUnityValidation.Run` in the scratch
project without `-quit`; screenshots go to `Temp/CampaignSelectionValidation`.
