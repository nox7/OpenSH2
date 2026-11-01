# OpenSH2
An open-source engine that is capable of playing Stronghold 2 running through Unity. 

You must own the original game from Firefly Studios. This is simply an engine that runs files. In order to play Stronghold 2, you must legally own the game.
 
## Straying from Original Game
This engine will stray from the original game by fixing quirks, adding new settings for enemies (bigger kingdoms, more units, harder AI), etc. The intent is to stay "true enough" to the original game. Additionally, Lua modding will be supported.

## Clean-Room Design
There have been no reverse engineering or decompilation processes in the making of this engine. This project follows a clean-room practice in studying and recreating an interoperable software which is protected under US and EU interoperability clauses in copyright law.

## Reimplementation Notes
Studies, such as research on the file formats, will be found in the `Notes` directory.

## Current Status
The project currently has half the game selection UIs finished, .s2m files decoded and implemented, video players embedded, livery implemented, AI simulation partially implemented, maps fully rendered, animations implemented, some jobs and workers implemented, updated shaders and lighting.

<img width="1293" height="644" alt="image" src="https://github.com/user-attachments/assets/61124e41-80bc-4809-8834-6bde5ea2e765" />

<img width="1065" height="593" alt="image" src="https://github.com/user-attachments/assets/eacf8d45-f654-447c-9640-05630478fef4" />

<img width="1053" height="593" alt="image" src="https://github.com/user-attachments/assets/f9dde7ff-9357-467c-a678-a57b6feca9d1" />

<img width="1056" height="594" alt="image" src="https://github.com/user-attachments/assets/55cb9948-b2c6-46c4-87f6-b730e247d9b0" />

## Bugs Fixed - Or Will Be Fixed
The following bugs are known and will be fixed in OpenSH2.
### Map Editor
- The map editor shows the wrong lord name when using _Other Lord Kills_ trigger. Olaf shows "Player"
- The map editor shows the wrong lord name when using _Specific Lord kills_. Lord Barclay shows as Olaf and Olaf shows as Player
  - This is similar to the above bug and most likely is reading from the wrong lords enum (as lord indices are different for different triggers). Not all enums have the Player available in the cases.
- The map editor doesn't correct save the right flag numbers in the _Redirect Village Output_ action. The target flag number is saved as the source and vice versa.
