# Project Outline - OpenSH2
This project is an open-source implementation of an engine that will play the Stronghold 2 game files. It will run through Unity, but the majority of the game logic will be handled via the C# scripting system and no prerendered assets/maps will exist in the Unity editor. Unity will serve as a backbone rendering engine for this implementation only.

All game source is in C# and will be in the `Assets/Code` directory. We are using Unity 6.4 (6000.4.7f1) for this project.

## Video Files
Original Stronghold 2 files are in Bink video format (.bik files) and the runtime/SDK for this is proprietary and requires a license. Reading the files and using FFMPEG to convert them doesn't.

UI and terrain videos are decoded directly from the original `.bik` files by embedded FFmpeg. The native bridge and setup instructions are in `Tools/EmbeddedFFmpeg`; managed decoding and texture playback are in `Assets/Code/Video/EmbeddedFFmpeg`. No external FFmpeg executable or MP4/WebM conversion cache is required. The current native runtime supports Windows x64.

## Caching
Video conversion and its old CacheManager have been removed. Other asset-loading caches, such as converted models, remain with their respective asset loaders.

## Game Flow
The game flow starts with a UnityGame object just named "Main" in a default scene. The Main has a script component attached to it which is the `Assets/Code/Main.cs` file. This file instantiates `Assets/Code/Game.cs` and starts `Assets/Code/UI/Frontend/FrontendController.cs`. The frontend plays the logo and intro with streaming audio and click-to-skip, then opens the main menu. Startup does not convert or cache videos. Automatic test-map loading is disabled.

### Main Menu
The main menu is a UI that has videos looping in the background. There are two video player UIs going on here. The most background element is a video playing the "MainMenuBackground" video (in a loop). Layered on top of it will be another video player that players "MainMenuCurtainOpen". Once that "MainMenuCurtainOpen" is _finished_ then "MainMenuCurtainIdle" plays in its plays in loop.

Layered on top of both of these videos will be the actual interactable UI layer done later.

## S2M Binary File Format
The format is currently being documented and notes on how .s2m files are structured is in the `Notes/FileFormats/S2M` directory.
