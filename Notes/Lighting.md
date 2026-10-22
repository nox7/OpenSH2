# Runtime atmosphere

`SampleScene` has an **Atmosphere Controller** on **Main**, starting at 17:30.
`Main.Start` also creates a controller if a different scene does not have one.
No map assets or baked lighting are required. The atmosphere uses URP 17.4's
Render Graph; its renderer feature is installed in both `PC_Renderer` and
`Mobile_Renderer`.

## Customizing the look

Select **Main → Atmosphere Controller** in the Inspector. Changes preview in
the Editor and during Play mode. Disable the component to restore the previous
sun, skybox, ambient lighting, and built-in fog setting.

* **Time Of Day** uses hours: 0 is midnight, 6 sunrise, 12 noon, 18 sunset.
  The initial 17.5 setting gives warm, low sunlight.
* **Sun Azimuth** rotates the sun's path around the map. **Sun Intensity**,
  **Sun Color**, and **Sun Strength** control direct lighting. The strength
  curve is keyed by normalized time (0–1 over 24 hours).
* Sky, horizon, ambient, and fog gradients are keyed by daylight/elevation,
  from night at 0 to day at 1. The sun-color gradient uses positive sun
  elevation. **Sunset Color** adds a warm horizon and fog near sunrise/sunset.
* **Fog Density**, **Fog Base Height**, and **Fog Height Falloff** control the
  depth and vertical distribution of haze. Density stays constant below the
  base height and decreases exponentially above it. **Fog Start Distance**
  keeps nearby details clear; **Max Fog Opacity** preserves silhouettes.
* **Sun Ray Intensity** and **Anisotropy** control scattering. Higher
  anisotropy concentrates rays toward the sun. Rays use the main light's
  shadow map, allowing terrain, trees, and buildings to block the sunlight.
* Fog and rays can be toggled independently. Rays use **Fog Density** as
  their scattering density even when fog is disabled.
* **Ray Steps** trades GPU cost for smoother rays (8–96, default 32).
  Start with 8–16 on slower hardware. **Max Distance** limits both fog
  integration and ray tracing. Rays use hard shadow taps; integration
  softens their appearance without multiple soft-shadow taps at each step.
* **Show In Scene View** enables preview while editing. Game camera overlays,
  preview cameras, and reflection cameras are excluded from the fog/ray pass.

Distances and heights are **Unity world units**. If you change the map's
Game Scale, adjust atmosphere distances/heights to match and divide fog
density and height falloff by that scale factor.

Create reusable settings with **Assets → Create → OpenSH2 → Atmosphere
Profile** and assign the asset to the controller's **Profile** field. When
assigned, the profile replaces the inline Settings. Editing a profile changes
that shared asset, including during Play mode.

## Driving a day-night cycle

The controller supports an optional preview clock: enable **Advance Time**
and set **Day Length Seconds** for one complete day. Advancement runs only
in Play mode and uses scaled Unity delta time, so pausing the game pauses it.
Leave it off when a game simulation clock owns the time:

```csharp
using Assets.Code.Lighting;

// Set this from the simulation clock (hours wrap into [0, 24)).
AtmosphereController.Active.TimeOfDay = simulationHours;

// Weather systems can also change runtime settings.
AtmosphereController.Active.Settings.FogDensity = 0.012f;
```

Time changes update the directional sun, ambient colors, sky, fog palette,
and ray brightness together. The default curve switches sunlight off at
night. A moon, stars, weather transitions, and reflection-probe refreshes
are separate future features. Avoid another system simultaneously writing
`RenderSettings` or the controlled sun.

## Rendering details and limits

The atmosphere pass runs after opaque geometry/sky and before transparency
and post-processing. It reconstructs world positions from scene depth and
supports perspective and orthographic map cameras. Terrain and foliage now
cast shadows, write depth, and receive sunlight; foliage cutouts use the
same alpha test in the visible, depth, and shadow passes.
Both also supply a `DepthNormals` pass with the same geometry and alpha
coverage. This is required when SSAO requests a combined depth/normal prepass;
without it, the atmosphere sees missing terrain/leaves as distant background.
Ray scattering integrates bounded transmittance even when fog compositing is
disabled, avoiding unbounded brightness accumulation on long view rays.

Imported opaque models (including bark and bridges) default to matte,
nonmetallic diffuse materials. The current GR2 loader supplies diffuse textures
without PBR reflectance data, so generated materials suppress Lit's default
specular highlights and glossy environment contribution. This avoids a pale
sheen on shaded models. An explicit model `FallbackMaterial` retains its authored
lighting settings.

Water receives sunlight/shadows and applies a short height-fog integration
before alpha blending with the already-fogged opaque background. Custom
transparent shaders should use `Lighting/Shaders/TransparentFog.hlsl` in
the same way. Transparent surfaces do not occlude the ray pass.

Shadowed rays are limited by the active URP asset's **Shadow Distance** and
cascade resolution. Beyond that range, scattering smoothly becomes unshadowed.
For a larger landscape, increase Shadow Distance in `PC_RPAsset` or
`Mobile_RPAsset` as performance allows. Fog remains available without shadows;
visible shafts require shadow-casting geometry between the sun and the fog.
The sky includes a sun disc/glow but no cloud layer. Existing post-processing
volumes still control tone mapping and bloom.

The shader is referenced directly by the renderer feature, and the procedural
sky shader is in Resources, so both remain available in player builds.

## Depth regression check

Outside Play mode, select the PC quality level and run **OpenSH2 → Lighting →
Validate Atmosphere Depth**. The check renders the custom terrain and alpha-cutout
foliage with SSAO, both camera projection types, and fog/rays/both. Nearby geometry
inside the fog start distance must preserve its color, alpha holes must remain
open, and distant background must receive fog. Disabling the depth/normals pass
is tested as a negative control to reproduce the original washout.
