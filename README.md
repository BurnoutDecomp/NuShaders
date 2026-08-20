# NuShaders

Custom shaders for Burnout Paradise.

## Quick start

```
python nushaders.py doctor all        # what is installed, what is missing, how to fix it
python nushaders.py tools build-cli   # build nushaders.exe (the C# format tool)
python nushaders.py build bpr         # compile + pack for Remastered PC
python nushaders.py verify bpr        # verify if the packer still reproduces correct bytes
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

## Examples

```
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

```
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