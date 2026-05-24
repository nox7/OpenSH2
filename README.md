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

## Bugs Fixed - Or Will Be Fixed
The following bugs are known and will be fixed in OpenSH2.
### Map Editor
- The map editor shows the wrong lord name when using _Other Lord Kills_ trigger. Olaf shows "Player"
- The map editor shows the wrong lord name when using _Specific Lord kills_. Lord Barclay shows as Olaf and Olaf shows as Player
  - This is similar to the above bug and most likely is reading from the wrong lords enum (as lord indices are different for different triggers). Not all enums have the Player available in the cases.
- The map editor doesn't correct save the right flag numbers in the _Redirect Village Output_ action. The target flag number is saved as the source and vice versa.