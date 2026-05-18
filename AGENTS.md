# Project Outline - OpenSH2
This project is an open-source implementation of an engine that will play the Stronghold 2 game files. It will run through Unity, but the majority of the game logic will be handled via the C# scripting system and no prerendered assets/maps will exist in the Unity editor. Unity will serve as a backbone rendering engine for this implementation only.

All game source is in C# and will be in the `Assets/Code` directory. We are using Unity 6.4 (6000.4.7f1) for this project.

## Video Files
Original Stronghold 2 files are in Bink video format (.bik files) and the runtime/SDK for this is proprietary and requires a license. Reading the files and using FFMPEG to convert them doesn't.

We convert these files with our Converter and store MP4 versions in a `Cache/Videos` folder.