# Source/Executable/Recovered

HLSL for the shader programs Burnout Paradise **embeds in its executable** rather than
shipping in `SHADERS.BNDL` — recovered instruction-for-instruction from the Xbox 360
build's Xenos microcode.

`Source/Executable/*.fx` next door is Criterion's own source for the executable shaders,
numbered by slot. These files are a different artifact with a different provenance: each
was **decompiled from the retail X360 binary's microcode blobs**, re-expressed as D3D9
`vs_3_0` / `ps_3_0`, and its constant surface named from the console program's own CTAB.
Where the two overlap they are two revisions of the same shader (the numbered originals
are the later PC revision; these are what the console actually ran), so neither replaces
the other and both are kept.

Every file's banner carries its own evidence: the X360 addresses of the microcode
packages it came from, the annotated Xenos listing, the CTAB constant names, and the
`fxc` recipe. That banner is the decode of record — do not trim it.

| File | Programs recovered | Consumed by |
|---|---|---|
| `brn_corona.fx` | corona VS/PS | `pc/gcm/renderengine/CoronaProgramsPC.cpp` |
| `brn_im2dblit.fx` | Im2d depth blit + composite blit VS/PS | `Im2dBlitProgramsPC.cpp` |
| `brn_im3d.fx` | Im3d VS/PS | `Im3dProgramsPC.cpp` |
| `brn_lionblend.fx` | Lion blend VS/PS + z-fade PS | `LionBlendProgramsPC.cpp` |
| `brn_postfx_b4blur.fx` | 8 B4Blur packages (7 distinct images) | `PostFxB4BlurProgramsPC.cpp` |
| `brn_postfx_bloom.fx` | 6 bloom packages | `PostFxBloomProgramsPC.cpp` |
| `brn_postfx_composite.fx` | 1 shared VS + 12 composite PS permutations | `PostFxProgramsPC.cpp` |
| `brn_postfx_helper.fx` | 9/16/4-tap blur + depth-of-field | `PostFxHelperProgramsPC.cpp` |
| `brn_skid.fx` | tyre-mark trail VS/PS | `SkidProgramsPC.cpp` |
| `brn_suncorona.fx` | sun occlusion + sun flare PS | `SunCoronaProgramsPC.cpp` |

Those consumers are generated C++ leaves in the **BP-Decomp_Workflow** decompilation
(`b5-decomp/src/pc/gcm/renderengine/`): each holds the compiled bytecode as a byte array,
because these programs have no `SHADERS.BNDL` entry to convert and no PC counterpart to
port. So editing a file here does **not** change the game on its own — the bytecode has to
be recompiled and regenerated into its leaf.

Nothing in `nushaders.py` builds this directory. The Xenos disassembler and CTAB reader
the banners cite (`xenos.py`, `ctab.py`) live in the decompilation workspace at
`BP-Decomp_Workflow/tools/assets/shaders/`, alongside the `SHADERS.BNDL` porter that
consumes `Source/Bundle/`.
