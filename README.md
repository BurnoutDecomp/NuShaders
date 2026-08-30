# NuShaders

Custom shaders for Burnout Paradise.

## Quick start

```bash
python nushaders.py doctor all        # what is installed, what is missing, how to fix it
python nushaders.py tools build-cli   # build nushaders.exe (the C# format tool)
python nushaders.py build bpr         # compile + pack for Remastered PC
python nushaders.py verify bpr        # verify if the packer still reproduces correct bytes
```

## First-time setup

Before doing anything, run this command:

```
python nushaders.py reference status     # what is present, what is missing, what needs it
```

Supply these versions of `SHADERS.BNDL` yourself:
### To compile for Remastered
- Just the `SHADERS.BNDL` found in your game install folder
### To compile for Xbox 360
- Burnout_tcartwright 'Breaker Island' build
- 1.6 'Free Feburary' Content Update
- 1.8 'Cops n Robbers' Content Update

Then, import the reference bundles:
```bash
python nushaders.py reference import "D:/dumps/SHADERS.BNDL" --version Breaker
# or alternatively, let the tool auto-detect the Remastered version
python nushaders.py reference import "D:/dumps/SHADERS.BNDL"
# then regenerate the manifest for your desired platform
python nushaders.py manifest regen --platform x360
```

`reference import` uses YAP to unpack the bundles into their respective directories.

Note: For Xbox 360, only `Breaker` has a resource string table. 
`manifest regen` will only work when that version of SHADERS.BNDL is provided.

Then check the toolchain:

```bash
python nushaders.py doctor all
```

## Layout

| Path | What |
|---|---|
| `Source/Bundle/gamedb/burnout5/Shaders` | the shader source |
| `Source/Bundle/gamedb/burnout5/Include` | shared shader headers |
| `Reference/` | where to place the extracted stock bundles, + format docs |
| `Build/manifest/` | generated shader ↔ resource-id map |
| `Build/config/` | per-machine deploy configs |
| `Build/Output/` | build artifacts |
| `Tools/NuBuild/` | the build system |
| `Tools/ShaderApp/` | the C# NuShaders app (`nushaders.exe`, GUI, format tests) |
| `Tools/Mod/SSRHook/` | (broken) SSR proxy DLL mod |

## Platforms

`D_PLATFORM_*` defines platform specific behavior. `Include/Platform.fxh` switches on:

| Target | Profile | Notes |
|---|---|---|
| `x360` | Xenos SM3 (`vs_3_0`/`ps_3_0`) | needs XDK 6534 |
| `bpr` | SM5 (`vs_5_0`/`ps_5_0`) | uses standard Windows SDK fxc |
| `pc-tub` | DX9 `fx_2_0` | the actual game uses the source direcly |
| `ps3` | — | not implemented yet |

`build` compiles each technique's VS and PS separately and packs them into
ShaderProgramBuffer resources. `build <platform> --effects --all-variants`
compiles all variants to ensure they properly compile, but don't work ingame.

## D_ROAD_X360 — console-authentic road shading

The road/tunnel `.fx` source in this repo is Criterion's later PC revision; the
shipped Xbox 360 game used different math, so roads render wrong when the PC
source is compiled as-is. `-D D_ROAD_X360` (or the `x360roads` variant on the
`x360` / `pc-tub` targets) switches the six road-family shaders —
`Road_Detailmap_*`, both `DriveableSurface_*`, and the three `Tunnel_*` road
variants — to the behaviour reverse engineered from the retail Breaker
`SHADERS.BNDL` microcode:

- light combine is `lerp(indirect, KeyLightColour, factor)` instead of
  `indirect + KeyLightColour*factor` (the PC add double-counts ambient in
  sun — the biggest visual difference);
- `CalcShadowFactor3CSM_X360_Aniso` gets its real decompiled body (2-point
  anisotropic PCF with a cascade-seam guard and a 0.75-texel kernel floor)
  instead of stubbing to a single `tex2Dproj`; open-road shaders
  (`SHADOW_APPLY_FADE_ROAD`) settle at `ShadowMap_Constants2.y` beyond the
  fade distance, tunnels fade to zero;
- `Road`/`Tunnel_Road` drop the tangent-space road normal mapping (the console
  shipped the `D_DISABLE_ROAD_SHADER` branch, no s3/s4, no tangent
  interpolator) and use the interpolated vertex N·L;
- `DriveableSurface_DetailMap_Diffuse` drops the Oren-Nayar path;
- `Tunnel_Lightmapped_*2` uses a hard-coded `lightmap * 2` white level;
- road/tunnel shaders write the direct-light factor to dest alpha.

Only shaders that opt in (`SHADOW_ROAD_X360_USER`) are affected; every other
shader compiles byte-identically with or without the flag. Per-shader evidence
lives in `scratch/x360_road_shaders/*/REPORT.md` in the parent workspace.

```bash
python nushaders.py build pc-tub --variant x360roads   # compile check
python nushaders.py build x360 -D D_ROAD_X360          # console-authentic roads
```

## Examples

```bash
# compile one shader
python nushaders.py build bpr --filter "Specular_1Bit_Doublesided.fx"

# compile all X360 shaders, then look at what changed
python nushaders.py build x360
python nushaders.py manifest show --platform x360

# set this machine up to deploy to BPR, then deploy
python nushaders.py config new
python nushaders.py config set bpr.game_exe "C:/.../BurnoutPR.exe"
python nushaders.py deploy bpr

# set up to deploy to xbox 360, then deploy
python nushaders.py config discover-ip --write
python nushaders.py deploy x360
python nushaders.py autotest --script shadertest.lua

# put a Remastered install back the way it was
python nushaders.py deploy --restore
```

`--dry-run` works on everything and prints what runs without
touching anything.

## The manifest

`Build/manifest/{x360,bpr}.json` maps each shader technique to the resource id
the game expects, derived from the stock bundle's `.debug.xml` plus
`Reference/ResourceDB.json`.

```bash
python nushaders.py manifest regen     # re-derive and write
python nushaders.py manifest check     # re-derive and diff; non-zero on drift
```

A technique whose annotation carries `string sharedName="..."` maps to **one**
engine resource however many `.fx` declare it — `ZOnlyOpaqueSingleSided` appears
in 55 shaders and has a single id. The manifest collapses those to one build
action. Where the copies disagree it records every distinct implementation under
`conflicts` and marks one `canonical`; `regen` never overwrites a `canonical`
someone set by hand.

## Known issues

- **Only 2 shaders currently compile for BPR.** `Specular_1Bit_Doublesided` and
  `Specular_1Bit_Doublesided_Parallax` are the only ones I got working in BPR, as a 
  majority of the surfaces I was testing at the time of creating NuShaders used that 
  shader. Finishing the port just needs figuring out where BPR's registers are for 
  each shader and adjusting the source accordingly.
- **`ZOnly1BitDoubleSided` has three different implementations** across the files 
  its present in, and those variants all share one resource ID.