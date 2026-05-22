# OpenSH2
An open-source engine that is capable of playing Stronghold 2 running through Unity. 

You must own the original game from Firefly Studios. This is simply an engine that runs files. In order to play Stronghold 2, you must legally own the game.

## Dependencies
- FFMPEG
  - For converting .bik videos into .mp4
 
## Straying from Original Game
This engine will stray from the original game by fixing quirks, adding new settings for enemies (bigger kingdoms, more units, harder AI), etc. The intent is to stay "true enough" to the original game. Additionally, Lua modding will be supported.

## Reimplementation Notes
Studies, such as research on the file formats, will be found in the `Notes` directory.

## Current Status
The project currently can play the intro videos (after auto-converting them to MP4 and WebM for alpha) and has the start of a UI library for runtime rendering the TGA textures as UI. Alignment and scaling is being worked on and in parallel so is .s2m binary format decoding.

<img width="1912" height="987" alt="image" src="https://github.com/user-attachments/assets/e5730f8b-00a3-49be-9d5b-af50a28fbe85" />
