# Project Outline - OpenSH2
This project is an open-source implementation of an engine that will play the Stronghold 2 game files. It will run through Unity, but the majority of the game logic will be handled via the C# scripting system and no prerendered assets/maps will exist in the Unity editor. Unity will serve as a backbone rendering engine for this implementation only.

All game source is in C# and will be in the `Assets/Code` directory. We are using Unity 6.4 (6000.4.7f1) for this project.

## Video Files
Original Stronghold 2 files are in Bink video format (.bik files) and the runtime/SDK for this is proprietary and requires a license. Reading the files and using FFMPEG to convert them doesn't.

UI and terrain videos are decoded directly from the original `.bik` files by embedded FFmpeg. The native bridge and setup instructions are in `Tools/EmbeddedFFmpeg`; managed decoding and texture playback are in `Assets/Code/Video/EmbeddedFFmpeg`. No external FFmpeg executable or MP4/WebM conversion cache is required. The current native runtime supports Windows x64.

## Caching
Video conversion and its old CacheManager have been removed. Other asset-loading caches, such as converted models, remain with their respective asset loaders.

## Game Flow
The game flow starts with a UnityGame object just named "Main" in a default scene. The Main has a script component attached to it which is the `Assets/Code/Main.cs` file. This file instantiates `Assets/Code/Game.cs` and starts `Assets/Code/UI/Frontend/FrontendController.cs`. The frontend plays the logo and intro with streaming audio and click-to-skip, then opens the main menu. Startup does not convert or cache videos. Maps are loaded only through UI initiation; the old hardcoded test-map harness has been removed. Campaign Play Mission maps chapters 1–12 to the installed `maps/war_chapterX.s2m` files through `WarCampaignMaps`, then calls `Main.LoadMapAsync`. Loading uses `GameState.LOADING_MAP`; simulation begins in `PLAYING` after initialization succeeds.

### Main Menu
The main menu is a UI that has videos looping in the background. There are two video player UIs going on here. The most background element is a video playing the "MainMenuBackground" video (in a loop). Layered on top of it will be another video player that players "MainMenuCurtainOpen". Once that "MainMenuCurtainOpen" is _finished_ then "MainMenuCurtainIdle" plays in its plays in loop.

Layered on top of both videos is the UI Toolkit menu. Play opens the war/peace selection page; its lower-left arrow returns to the title. Path of Peace opens `PathOfPeaceSelection`, with Sim Campaign, Free Build and Custom Maps previews/descriptions; its Back arrow returns to the chooser. Path of War opens `PathOfWarSelection`, with Campaign, Kingmaker, Siege, Conquest Trail and Custom previews/descriptions and the same Back navigation. Page controllers are organized under `Assets/Code/UI/Screens`, with matching UXML/USS under `Assets/Code/UI/Resources/OpenSH2/Frontend/Screens`. The router creates pages lazily and shares the centered 4:3 viewport and stone borders. Frontend navigation stays separate from map loading and simulation.

## S2M Binary File Format
The format is currently being documented and notes on how .s2m files are structured is in the `Notes/FileFormats/S2M` directory.

## Local User Data
Versioned per-OS-user configuration/progress is in `Assets/Code/UserData`, stored at
`%LOCALAPPDATA%/OpenSH2/profile.json` separately from save files. Campaign selection
is under `UI/Screens/CampaignSelection` with matching UXML/USS; War's Campaign button
opens it. The temporary `CampaignProgress.CompleteAllForLocalTesting` override makes
all chapters completed for current local testing without changing stored progress.
Campaign launch records attempts through `UserProfileStore`; victory handling must call
`CompleteWarChapter` once implemented. Chapter selection alone must not change progress.

## Local Player Camera
After map initialization, `Main` creates `Player/LocalPlayerSession`, assigns its
camera to `buildingRenderSettings.ViewCamera`, then enters PLAYING. Tune the live
serialized `PlayerCameraSettings` on Main for angle/FOV, speed and ray-distance
zoom limits. WASD follows the camera's heading on the ground plane (pitch does not
add altitude); wheel input zooms in customizable steps (default 40 map units). Q/E
and horizontal middle-button drag rotate the heading at customizable speeds. Panning
preserves altitude unless a surface-ray zoom limit is violated. Movement/keyboard
rotation use unscaled time. Pan speed ramps linearly from Initial Move Speed to
Max Move Speed over Move Speed Interpolation Time; releasing movement resets the ramp. `CameraSurface` marks only
terrain/building/wall renderer roots for surface ray hits, excluding foliage and
actors. Map cleanup restores the scene camera and previous building-camera override.

Camera rotation uses a separate terrain-only centre-ray pivot. Terrain roots mark
`CameraSurface.IsTerrain`; buildings still constrain height/zoom but never define
the orbit centre. Rays missing terrain use the lowest-terrain-height plane beyond
the map edge, without clamping the rotation pivot to the map boundary.
