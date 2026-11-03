# Local player camera

`Main` initializes `LocalPlayerSession` after the map world is ready and before
entering PLAYING. This creates a dedicated tagged MainCamera, suspends the scene
camera/listener, and assigns `Building Render Settings > View Camera` to the new
camera. Map teardown releases the player camera and restores the previous camera,
listener and building-camera override. No HUD or unit-selection controls are added yet.

Tune **Main > Player Camera Settings** in the Inspector. Pitch (default 50 degrees),
yaw, perspective field of view (35 degrees), pan acceleration, zoom step and distance
limits are editable live. Initial Surface Distance is applied on map load.
Distances/speeds use map units and follow Main's Game Scale. Zoom limits measure
along the centre viewing ray to a surface, rather than vertical world height.

W/S move along the camera's horizontal forward axis; A/D move along its right
axis. Pitch never adds vertical movement. Diagonal movement is normalized.
Pan speed interpolates linearly from Initial Move Speed (default 30 map units/sec)
to Max Move Speed (60) over Move Speed Interpolation Time (1 second). Releasing
movement resets the ramp; changing direction while still moving keeps it running.
Zero interpolation time uses maximum speed immediately. Travel integrates the
ramp across frames, including frames crossing its end, for frame-independent motion.
The former serialized Move Speed value migrates to Initial Move Speed.
Wheel up zooms in and wheel down zooms out. Zoom Speed defaults to 40 map units
per notch (five times the original step) and remains customizable. Q/E orbit left/right
around the terrain point beneath the centre viewing ray; holding the middle mouse button and dragging
horizontally also rotates the heading, with drag directions reversed from the initial implementation. Keyboard Rotation Speed defaults to 90 degrees
per second and Mouse Rotation Sensitivity to 0.2 degrees per pixel. Pitch stays fixed.
Movement and keyboard rotation use unscaled frame time,
so pausing/speed changes do not freeze or accelerate the camera. The focus stays
inside the map's world bounds when panning; an off-map rotation pivot can move
back toward the map without snapping to the boundary. Keyboard/mouse input is read only during PLAYING
and while the application has focus. New Input System and legacy input are supported.

`CameraSurface` explicitly opts geometry into camera rays. Terrain, building and
wall renderers attach it to their own roots, including newly constructed building
roots. Foliage, grass, water and actors remain excluded even if they have colliders.
Rays select the nearest opted-in non-trigger collider belonging to this map; the
non-alloc hit buffer grows when needed so dense excluded geometry cannot hide terrain.
Panning preserves camera altitude over changing terrain/roof heights. The filtered centre
ray's actual hit distance is measured each frame; height changes only if that distance
would violate a zoom limit, or the player explicitly zooms/adjusts pitch. Re-anchoring
the view centre does not move the camera while its distance remains within limits.

`S2MPlayerCameraStart` picks the local human player's keep when ownership resolves,
then an active actor assigned to that player, otherwise the map centre. Authored
map-camera records are not decoded yet.

`Notes/tools/PlayerCameraUnityValidation.cs` exercises yaw-relative horizontal
movement, zoom clamps, diagonal speed, map scaling, live pitch/FOV, map bounds,
terrain/building hits through 70 excluded colliders, camera assignment, actual
campaign launch and cleanup. Run in an isolated Unity project with code/settings,
native plugins and the built Granny2 converter using
`-batchmode -executeMethod PlayerCameraUnityValidation.Run` without `-quit`.
Results and a rendered mission view are in `Temp/PlayerCameraValidation`.

Rotation uses a separate terrain-only query. Terrain renderer roots set
`CameraSurface.IsTerrain`; buildings/walls keep it false while still participating
in height/zoom constraints. A roof therefore never becomes the rotation pivot.
When the forward ray misses terrain, its intersection with a horizontal plane at
the lowest terrain collider height is the virtual pivot, including beyond map edges.
`Focus` reports this terrain/virtual pivot; `SurfaceDistance` remains the filtered
terrain/building ray distance. Height correction moves along the viewing ray and
preserves the terrain rotation centre.
