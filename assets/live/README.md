# Local scene animation assets

`Planets.hlsl` is the source for the WPF Shader Model 3 effect. Rebuild
`Planets.ps` with `python tools/compile_planet_shader.py` on Windows using
the inbox Direct3D compiler; no paid service or model is involved.

The RGB masks preserve foreground characters, clouds and planet silhouettes:
red = first planet, green = second planet. Blue is unused. The deterministic
surface texture wraps in longitude. Assets originate from VWP's archived
local motion prototype; the image-wide deformation effect is not used.

All actors are code-native drawings in SceneActors.cs, rendered over the
existing composition. No credentials, runtime network access, downloaded
character models, or generated video are required.
