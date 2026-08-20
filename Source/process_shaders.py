import os
import re
import shutil
import sys

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
BUNDLE_DIR = os.path.join(BASE_DIR, "Bundle", "gamedb", "burnout5")
SHADERS_DIR = os.path.join(BUNDLE_DIR, "Shaders")
INCLUDE_DIR = os.path.join(BUNDLE_DIR, "Include")
EXECUTABLE_DIR = os.path.join(BASE_DIR, "Executable")
BACKUP_DIR = os.path.join(BASE_DIR, "_backup")

PREPROCESSOR_HEADER = (
    "// These defines were added by the ShaderPreProcessor tool to allow it to work\n"
    "#define PREPROCESSOR_DEFINE_TRUE\n"
    "// End of Added Defines\n"
)

# ---- Block content patterns (for matching start/end of extractable blocks) ----

TRANSFORM_START = re.compile(r'^float4x4\s+viewProjection\s*:\s*ViewProjection\s*$')
TRANSFORM_END_FUNC = 'GetViewSpaceDepthFromWorldPosition'

SHADOW_START = re.compile(r'^float4x4\s+ShadowMap_WorldToLight\[3\]\s*$')
SHADOW_END_FUNCS = [
    'CalcShadowFactorCSM_Vehicle_Damaged_2CSM_Select',
    'CalcShadowFactor1CSM',
    'CalcShadowFactor2CSMSelect',
    'CalcShadowFactor2CSM',
    'CalcShadowFactor3CSM',
]

FOG_START = re.compile(r'^float4\s+ScattCoeffs\s*$')
FOG_END_FUNC = 'CalculateScattering'

IRRADIANCE_START = re.compile(r'^float4x4\s+IrradianceQuadricA\s*$')
IRRADIANCE_END_FUNC = 'ComputeIrradianceFast'

DEPTH_ENCODE_START = re.compile(r'^#if\s+.*D_MRT.*$')
DEPTH_ENCODE_FUNC = 'ConvertDepthToARGB'

NORMAL_MAP_START_FUNC = 'DecodeNormalMap'
NORMAL_MAP_END_FUNC = 'TransformTangetSpaceNormalToWorldSpaceNormal'


def find_all_fx_files(directory):
    result = []
    for root, dirs, files in os.walk(directory):
        for f in files:
            if f.endswith('.fx'):
                result.append(os.path.join(root, f))
    return sorted(result)


def find_function_end(lines, start_idx):
    """Find the closing brace of a function/block starting at start_idx.
    Tracks brace nesting."""
    depth = 0
    found_open = False
    for i in range(start_idx, len(lines)):
        for ch in lines[i]:
            if ch == '{':
                depth += 1
                found_open = True
            elif ch == '}':
                depth -= 1
                if found_open and depth == 0:
                    return i
    return start_idx


def find_block_by_regex_and_func_end(lines, start_re, end_func_name):
    """Find a block that starts with a regex match and ends at the closing brace
    of a specific function."""
    start_idx = None
    for i, line in enumerate(lines):
        if start_re.match(line.strip()):
            start_idx = i
            break
    if start_idx is None:
        return None, None

    end_idx = None
    for i in range(start_idx, len(lines)):
        if end_func_name in lines[i]:
            end_idx = find_function_end(lines, i)
            break
    if end_idx is None:
        return start_idx, None
    return start_idx, end_idx


def find_shadow_block(lines):
    """Find the shadow block. It starts at ShadowMap_WorldToLight and ends at the
    last CalcShadowFactor function's closing brace."""
    start_idx = None
    for i, line in enumerate(lines):
        if SHADOW_START.match(line.strip()):
            start_idx = i
            break
    if start_idx is None:
        return None, None

    last_func_end = None
    for func_name in SHADOW_END_FUNCS:
        for i in range(start_idx, len(lines)):
            stripped = lines[i].strip()
            if func_name in stripped and ('float' in stripped or 'void' in stripped or func_name + '(' in stripped or func_name + '\n' in lines[i]):
                candidate = find_function_end(lines, i)
                if candidate is not None:
                    if last_func_end is None or candidate > last_func_end:
                        last_func_end = candidate

    # Also look for the ApplyFade function end (it's between GetShadowMapPositions and CalcShadowFactor)
    for i in range(start_idx, len(lines)):
        if 'ApplyFade' in lines[i] and 'float' in lines[i]:
            af_end = find_function_end(lines, i)
            if af_end is not None and last_func_end is not None and af_end > last_func_end:
                pass  # ApplyFade is before CalcShadowFactor, should be included

    # But we also need to capture the #if defined(D_SOFT_SHADOWS) ... #endif block
    # and the SHADOWMAP_INTERPOLATORS macros that precede the functions.
    # The start_idx already captures from ShadowMap_WorldToLight, and the macro block
    # (#if defined... #define SHADOWMAP_INTERPOLATORS...) follows the sampler declaration.
    # We also need to capture up through any #endif that closes the macro block.

    # Find the actual last line: look for the closing brace of the very last shadow function
    if last_func_end is None:
        return start_idx, None
    return start_idx, last_func_end


def find_depth_encode_block(lines):
    """Find the ConvertDepthToARGB block inside #ifdef D_MRT."""
    for i, line in enumerate(lines):
        stripped = line.strip()
        if re.match(r'^#if\b.*D_MRT', stripped) and i + 1 < len(lines) and 'ConvertDepthToARGB' in lines[i + 1]:
            # Find matching #endif
            depth = 1
            for j in range(i + 1, len(lines)):
                s = lines[j].strip()
                if s.startswith('#if'):
                    depth += 1
                elif s == '#endif':
                    depth -= 1
                    if depth == 0:
                        return i, j
    return None, None


def find_normal_map_block(lines):
    """Find the normal mapping functions block (DecodeNormalMap through TransformTangetSpaceNormalToWorldSpaceNormal)."""
    start_idx = None
    for i, line in enumerate(lines):
        if NORMAL_MAP_START_FUNC in line and 'float3' in lines[max(0, i-1):i+1][0] if i > 0 else False:
            start_idx = i - 1  # include the return type line
            break
        elif NORMAL_MAP_START_FUNC + '(' in line:
            # Check if previous line has float3 return type
            if i > 0 and lines[i-1].strip().startswith('float3'):
                start_idx = i - 1
            else:
                start_idx = i
            break
    if start_idx is None:
        return None, None

    end_idx = None
    for i in range(start_idx, len(lines)):
        if NORMAL_MAP_END_FUNC in lines[i]:
            end_idx = find_function_end(lines, i)
            break
    return start_idx, end_idx


def detect_shadow_zbias(lines, shadow_start, shadow_end):
    """Detect the z-bias value used in GetShadowMapPositions functions."""
    if shadow_start is None:
        return None
    for i in range(shadow_start, shadow_end + 1 if shadow_end else len(lines)):
        m = re.search(r'position\d\.z\s*-=\s*([\d.]+)\s*\*\s*ShadowMap_Constants2\.z', lines[i])
        if m:
            return m.group(1)
    return None


def detect_shadow_zbias_calc(lines, shadow_start, shadow_end):
    """Detect if CalcShadowFactor3CSM has a z-bias on metaMapTexCoord."""
    if shadow_start is None:
        return None
    in_calc3 = False
    for i in range(shadow_start, shadow_end + 1 if shadow_end else len(lines)):
        if 'CalcShadowFactor3CSM' in lines[i] and 'float' in lines[i]:
            in_calc3 = True
        if in_calc3:
            m = re.search(r'metaMapTexCoord\.z\s*-=\s*([\d.]+)\s*\*\s*ShadowMap_Constants2\.z', lines[i])
            if m:
                return m.group(1)
            if in_calc3 and lines[i].strip() == '}':
                break
    return None


def detect_road_applyfade(lines, shadow_start, shadow_end):
    """Detect if this file uses the road variant of ApplyFade."""
    if shadow_start is None:
        return False
    for i in range(shadow_start, shadow_end + 1 if shadow_end else len(lines)):
        if 'ApplyFade' in lines[i] and 'float' in lines[i]:
            # Check next few lines for the road variant
            for j in range(i, min(i + 5, len(lines))):
                if 'saturate( factor )' in lines[j] and 'ShadowMap_Constants2.y' in lines[j]:
                    return True
            break
    return False


def get_include_prefix(fx_path):
    """Determine the relative path prefix for #include based on file location."""
    rel = os.path.relpath(fx_path, BUNDLE_DIR)
    parts = rel.replace('\\', '/').split('/')
    # Parts like: Shaders/file.fx -> depth 1 from burnout5
    # Playground/SkinTest/file.fx -> depth 2
    # Playground/Test_Shaders/file.fx -> depth 2
    depth = len(parts) - 1  # subtract the filename
    if depth == 1:
        return "../Include"
    elif depth == 2:
        return "../../Include"
    else:
        # Compute relative path from file dir to Include dir
        file_dir = os.path.dirname(fx_path)
        return os.path.relpath(INCLUDE_DIR, file_dir).replace('\\', '/')


# ============================================================================
# Phase 1: PREPROCESSOR_DEFINE_TRUE cleanup
# ============================================================================

def phase1_cleanup_preprocessor(content):
    """Remove PREPROCESSOR_DEFINE_TRUE marker and simplify all conditionals."""
    # Remove the 3-line header block
    content = content.replace(PREPROCESSOR_HEADER, '')

    # Pattern: #if defined(PREPROCESSOR_DEFINE_TRUE) && defined(X)  ->  #ifdef X
    content = re.sub(
        r'#if\s+defined\s*\(\s*PREPROCESSOR_DEFINE_TRUE\s*\)\s*&&\s*defined\s*\(\s*(\w+)\s*\)',
        r'#ifdef \1',
        content
    )

    # Pattern: #elif defined(PREPROCESSOR_DEFINE_TRUE) && defined(X)  ->  #elif defined(X)
    content = re.sub(
        r'#elif\s+defined\s*\(\s*PREPROCESSOR_DEFINE_TRUE\s*\)\s*&&\s*defined\s*\(\s*(\w+)\s*\)',
        r'#elif defined(\1)',
        content
    )

    # Pattern: #if defined(PREPROCESSOR_DEFINE_TRUE) && ( defined(X) || defined(Y) )
    content = re.sub(
        r'#if\s+defined\s*\(\s*PREPROCESSOR_DEFINE_TRUE\s*\)\s*&&\s*\(\s*defined\s*\(\s*(\w+)\s*\)\s*\|\|\s*defined\s*\(\s*(\w+)\s*\)\s*\)',
        r'#if defined(\1) || defined(\2)',
        content
    )

    # Pattern: #elif defined(PREPROCESSOR_DEFINE_TRUE) && ( defined(X) || defined(Y) )
    content = re.sub(
        r'#elif\s+defined\s*\(\s*PREPROCESSOR_DEFINE_TRUE\s*\)\s*&&\s*\(\s*defined\s*\(\s*(\w+)\s*\)\s*\|\|\s*defined\s*\(\s*(\w+)\s*\)\s*\)',
        r'#elif defined(\1) || defined(\2)',
        content
    )

    return content


def phase1_cleanup_executable_special(content):
    """Handle the special D_PLATFORM_X360 || PREPROCESSOR_DEFINE_TRUE pattern.
    Since PREPROCESSOR_DEFINE_TRUE was always true, these blocks were always compiled.
    Make the code unconditional."""
    lines = content.split('\n')
    result = []
    i = 0
    while i < len(lines):
        stripped = lines[i].strip()
        # Check for #if defined(D_PLATFORM_X360) || defined(PREPROCESSOR_DEFINE_TRUE)
        if re.match(r'#if\s+defined\s*\(\s*D_PLATFORM_X360\s*\)\s*\|\|\s*defined\s*\(\s*PREPROCESSOR_DEFINE_TRUE\s*\)', stripped) or \
           re.match(r'#if\s+defined\s*\(\s*D_PLATFORM_X360\s*\)\s*\|\|\s*defined\s*\(\s*PREPROCESSOR_DEFINE_TRUE\s*\)', stripped.replace(' (', '(')):
            # Find matching #endif (and possible #else)
            depth = 1
            block_lines = []
            else_lines = []
            in_else = False
            j = i + 1
            while j < len(lines) and depth > 0:
                s = lines[j].strip()
                if s.startswith('#if'):
                    depth += 1
                    if in_else:
                        else_lines.append(lines[j])
                    else:
                        block_lines.append(lines[j])
                elif s == '#endif':
                    depth -= 1
                    if depth == 0:
                        j += 1
                        break
                    else:
                        if in_else:
                            else_lines.append(lines[j])
                        else:
                            block_lines.append(lines[j])
                elif s.startswith('#else') and depth == 1:
                    in_else = True
                elif s.startswith('#elif') and depth == 1:
                    in_else = True
                    if in_else:
                        else_lines.append(lines[j])
                    else:
                        block_lines.append(lines[j])
                else:
                    if in_else:
                        else_lines.append(lines[j])
                    else:
                        block_lines.append(lines[j])
                j += 1
            # Keep only the first branch (always-true path), discard #else branch
            result.extend(block_lines)
            i = j
        else:
            result.append(lines[i])
            i += 1
    return '\n'.join(result)


# ============================================================================
# Phase 2: Create header files
# ============================================================================

def create_header_files(canonical_lines):
    """Create all .fxh header files from the canonical file content."""
    os.makedirs(INCLUDE_DIR, exist_ok=True)

    # --- Transform.fxh ---
    t_start, t_end = find_block_by_regex_and_func_end(canonical_lines, TRANSFORM_START, TRANSFORM_END_FUNC)
    transform_content = '\n'.join(canonical_lines[t_start:t_end + 1]) + '\n'
    write_header('Transform.fxh', transform_content)

    # --- Shadow.fxh ---
    s_start, s_end = find_shadow_block(canonical_lines)
    shadow_lines = canonical_lines[s_start:s_end + 1]
    shadow_content = '\n'.join(shadow_lines) + '\n'

    # Now parameterize the shadow content for z-bias
    # Add z-bias support at the top, then insert bias lines conditionally
    shadow_header = (
        "#ifndef SHADOW_FXH\n"
        "#define SHADOW_FXH\n\n"
    )

    # The canonical file (Diffuse_Opaque_Singlesided) has NO z-bias,
    # so we need to add the z-bias lines conditionally.
    # We'll insert them into the GetShadowMapPositions functions and CalcShadowFactor3CSM.
    shadow_body = shadow_content

    # For GetShadowMapPositions2CSMSelect: after texCoord1 = float4( position1, ... )
    # add z-bias for both positions
    # We need to handle this carefully - the canonical has no bias, but some files do.
    # Strategy: add conditional bias after each 'position0 = mul(...)' pattern in the
    # GetShadowMapPositions functions.

    # Insert z-bias support. We'll add it as conditional blocks.
    # After each line like "float3 position0 = mul(...).xyz;" in GetShadowMapPositions*,
    # if it's followed by a texCoord assignment, insert bias before that assignment.

    # Actually, the simplest approach: add the bias insertion points manually
    # since we know the exact structure from the canonical file.

    # For GetShadowMapPositions2CSMSelect: positions at lines with texCoord0/texCoord1 assignment
    # The canonical has no bias. We need to add conditional bias.

    # Let's build the shadow header content directly with parameterization
    shadow_parameterized = shadow_header + shadow_body + "\n#endif\n"

    # Now insert z-bias lines. We'll process line by line.
    shadow_out_lines = []
    in_get_positions_func = False
    current_func = None
    brace_depth = 0
    func_brace_start = False

    for line in shadow_parameterized.split('\n'):
        stripped = line.strip()

        # Detect entering a GetShadowMapPositions function
        if any(fname in stripped for fname in ['GetShadowMapPositions2CSMSelect(', 'GetShadowMapPositions2CSMSelectVS(',
                                                'GetShadowMapPositions3CSM(', 'GetShadowMapPositions3CSM_4(',
                                                'GetShadowMapPositions2CSM(', 'GetShadowMapPositions1CSM(']):
            if 'void' in stripped or (len(shadow_out_lines) > 0 and 'void' in shadow_out_lines[-1]):
                for fname in ['GetShadowMapPositions2CSMSelect', 'GetShadowMapPositions2CSMSelectVS',
                              'GetShadowMapPositions3CSM_4', 'GetShadowMapPositions3CSM',
                              'GetShadowMapPositions2CSM', 'GetShadowMapPositions1CSM']:
                    if fname + '(' in stripped:
                        current_func = fname
                        break
                in_get_positions_func = True
                func_brace_start = False
                brace_depth = 0

        if in_get_positions_func:
            for ch in stripped:
                if ch == '{':
                    brace_depth += 1
                    func_brace_start = True
                elif ch == '}':
                    brace_depth -= 1

        shadow_out_lines.append(line)

        # Insert z-bias after position assignment lines
        if in_get_positions_func and func_brace_start:
            if current_func == 'GetShadowMapPositions3CSM_4':
                pass  # This function just calls GetShadowMapPositions3CSM, no direct position assignment
            elif current_func == 'GetShadowMapPositions3CSM':
                # After position0 = mul(...), insert bias for position0 only
                if re.match(r'\s*float3\s+position0\s*=\s*mul\(', stripped):
                    shadow_out_lines.append('#ifdef SHADOW_APPLY_Z_BIAS')
                    shadow_out_lines.append('    position0.z -= SHADOW_Z_BIAS_VALUE * ShadowMap_Constants2.z;')
                    shadow_out_lines.append('#endif')
            elif current_func in ['GetShadowMapPositions2CSMSelectVS', 'GetShadowMapPositions2CSM']:
                # After position1 = mul(...), insert bias for both
                if re.match(r'\s*float3\s+position1\s*=\s*mul\(', stripped):
                    shadow_out_lines.append('#ifdef SHADOW_APPLY_Z_BIAS')
                    shadow_out_lines.append('    position0.z -= SHADOW_Z_BIAS_VALUE * ShadowMap_Constants2.z;')
                    shadow_out_lines.append('    position1.z -= SHADOW_Z_BIAS_VALUE * ShadowMap_Constants2.z;')
                    shadow_out_lines.append('#endif')
            elif current_func == 'GetShadowMapPositions1CSM':
                # After position0 = mul(...), insert bias
                if re.match(r'\s*float3\s+position0\s*=\s*mul\(', stripped):
                    shadow_out_lines.append('#ifdef SHADOW_APPLY_Z_BIAS')
                    shadow_out_lines.append('    position0.z -= SHADOW_Z_BIAS_VALUE * ShadowMap_Constants2.z;')
                    shadow_out_lines.append('#endif')
            elif current_func == 'GetShadowMapPositions2CSMSelect':
                # After position1 = mul(...), insert bias for both
                if re.match(r'\s*float3\s+position1\s*=\s*mul\(', stripped):
                    shadow_out_lines.append('#ifdef SHADOW_APPLY_Z_BIAS')
                    shadow_out_lines.append('    position0.z -= SHADOW_Z_BIAS_VALUE * ShadowMap_Constants2.z;')
                    shadow_out_lines.append('    position1.z -= SHADOW_Z_BIAS_VALUE * ShadowMap_Constants2.z;')
                    shadow_out_lines.append('#endif')

        # Insert z-bias in CalcShadowFactor3CSM after metaMapTexCoord assignment
        if 'float3 metaMapTexCoord' in stripped and 'CalcShadowFactor' not in stripped:
            # Check if we're inside CalcShadowFactor3CSM (not the Vehicle_Damaged one)
            # Look back to find which function we're in
            for back_i in range(len(shadow_out_lines) - 1, max(0, len(shadow_out_lines) - 20), -1):
                if 'CalcShadowFactor3CSM' in shadow_out_lines[back_i] and 'Vehicle_Damaged' not in shadow_out_lines[back_i]:
                    shadow_out_lines.append('#ifdef SHADOW_APPLY_Z_BIAS')
                    shadow_out_lines.append('    metaMapTexCoord.z -= SHADOW_Z_BIAS_VALUE * ShadowMap_Constants2.z;')
                    shadow_out_lines.append('#endif')
                    break

        # Handle end of function
        if in_get_positions_func and func_brace_start and brace_depth == 0:
            in_get_positions_func = False
            current_func = None

    # Now handle ApplyFade parameterization
    final_shadow_lines = []
    for line in shadow_out_lines:
        stripped = line.strip()
        if 'return factor * fadeValue + (float)1 - fadeValue;' in stripped:
            final_shadow_lines.append('#ifdef SHADOW_APPLY_FADE_ROAD')
            final_shadow_lines.append('    return saturate( factor ) * fadeValue + ( (float)1 - fadeValue ) * (float)ShadowMap_Constants2.y; ')
            final_shadow_lines.append('#else')
            final_shadow_lines.append(line)
            final_shadow_lines.append('#endif')
        else:
            final_shadow_lines.append(line)

    with open(os.path.join(INCLUDE_DIR, 'Shadow.fxh'), 'w', newline='\n') as f:
        f.write('\n'.join(final_shadow_lines))
    print(f"  Created Shadow.fxh")

    # --- Fog.fxh ---
    f_start, f_end = find_block_by_regex_and_func_end(canonical_lines, FOG_START, FOG_END_FUNC)
    fog_content = '\n'.join(canonical_lines[f_start:f_end + 1]) + '\n'
    write_header('Fog.fxh', fog_content)

    # --- Irradiance.fxh ---
    ir_start, ir_end = find_block_by_regex_and_func_end(canonical_lines, IRRADIANCE_START, IRRADIANCE_END_FUNC)
    irradiance_content = '\n'.join(canonical_lines[ir_start:ir_end + 1]) + '\n'
    write_header('Irradiance.fxh', irradiance_content)

    # --- DepthEncode.fxh ---
    de_start, de_end = find_depth_encode_block(canonical_lines)
    depth_content = '\n'.join(canonical_lines[de_start:de_end + 1]) + '\n'
    write_header('DepthEncode.fxh', depth_content)

    # --- NormalMapping.fxh ---
    # Read from a file that has normal mapping
    nm_file = os.path.join(SHADERS_DIR, 'Diffuse_1Bit_Doublesided.fx')
    with open(nm_file, 'r') as f:
        nm_content = f.read()
    nm_content = phase1_cleanup_preprocessor(nm_content)
    nm_lines = nm_content.split('\n')

    nm_start, nm_end = find_normal_map_block(nm_lines)
    if nm_start is not None and nm_end is not None:
        nm_block = '\n'.join(nm_lines[nm_start:nm_end + 1]) + '\n'
        write_header('NormalMapping.fxh', nm_block)


def write_header(filename, content):
    """Write a header file with include guards."""
    guard = filename.upper().replace('.', '_')
    path = os.path.join(INCLUDE_DIR, filename)
    if filename == 'Shadow.fxh':
        # Shadow.fxh already has guards from the parameterization
        with open(path, 'w', newline='\n') as f:
            f.write(content)
    else:
        with open(path, 'w', newline='\n') as f:
            f.write(f"#ifndef {guard}\n")
            f.write(f"#define {guard}\n\n")
            f.write(content)
            f.write(f"\n#endif\n")
    print(f"  Created {filename}")


# ============================================================================
# Phase 3: Extract shared blocks from Bundle files
# ============================================================================

def phase3_extract_bundle(fx_path):
    """Extract shared blocks from a Bundle .fx file and replace with #include."""
    with open(fx_path, 'r') as f:
        content = f.read()

    # Phase 1 cleanup first
    content = phase1_cleanup_preprocessor(content)
    lines = content.split('\n')

    include_prefix = get_include_prefix(fx_path)

    # Detect which blocks exist
    has_normal_map = any(NORMAL_MAP_START_FUNC in line for line in lines)
    has_shadow = any(SHADOW_START.match(line.strip()) for line in lines)
    has_irradiance = any(IRRADIANCE_START.match(line.strip()) for line in lines)

    # Detect shadow variants
    zbias_value = None
    zbias_calc_value = None
    is_road_fade = False
    if has_shadow:
        s_start, s_end = find_shadow_block(lines)
        zbias_value = detect_shadow_zbias(lines, s_start, s_end)
        zbias_calc_value = detect_shadow_zbias_calc(lines, s_start, s_end)
        is_road_fade = detect_road_applyfade(lines, s_start, s_end)

    # Now find all block boundaries and build replacement map
    # We'll mark line ranges to remove and what to insert at each position
    removals = []  # list of (start, end, replacement_text)

    # Normal mapping (comes first in files that have it)
    if has_normal_map:
        nm_start, nm_end = find_normal_map_block(lines)
        if nm_start is not None and nm_end is not None:
            removals.append((nm_start, nm_end, f'#include "{include_prefix}/NormalMapping.fxh"'))

    # Transform
    t_start, t_end = find_block_by_regex_and_func_end(lines, TRANSFORM_START, TRANSFORM_END_FUNC)
    if t_start is not None and t_end is not None:
        removals.append((t_start, t_end, f'#include "{include_prefix}/Transform.fxh"'))

    # Shadow
    if has_shadow:
        s_start, s_end = find_shadow_block(lines)
        if s_start is not None and s_end is not None:
            shadow_defines = ''
            if zbias_value or zbias_calc_value:
                bias = zbias_value or zbias_calc_value
                shadow_defines = f'#define SHADOW_APPLY_Z_BIAS\n#define SHADOW_Z_BIAS_VALUE {bias}\n'
            if is_road_fade:
                shadow_defines += '#define SHADOW_APPLY_FADE_ROAD\n'
            replacement = shadow_defines + f'#include "{include_prefix}/Shadow.fxh"'
            removals.append((s_start, s_end, replacement))

    # Fog
    f_start, f_end = find_block_by_regex_and_func_end(lines, FOG_START, FOG_END_FUNC)
    if f_start is not None and f_end is not None:
        removals.append((f_start, f_end, f'#include "{include_prefix}/Fog.fxh"'))

    # Irradiance
    if has_irradiance:
        ir_start, ir_end = find_block_by_regex_and_func_end(lines, IRRADIANCE_START, IRRADIANCE_END_FUNC)
        if ir_start is not None and ir_end is not None:
            removals.append((ir_start, ir_end, f'#include "{include_prefix}/Irradiance.fxh"'))

    # DepthEncode
    de_start, de_end = find_depth_encode_block(lines)
    if de_start is not None and de_end is not None:
        removals.append((de_start, de_end, f'#include "{include_prefix}/DepthEncode.fxh"'))

    # Sort removals by start line (ascending)
    removals.sort(key=lambda x: x[0])

    # Validate no overlapping ranges
    for i in range(1, len(removals)):
        if removals[i][0] <= removals[i-1][1]:
            print(f"  WARNING: Overlapping blocks in {os.path.basename(fx_path)}: "
                  f"[{removals[i-1][0]}-{removals[i-1][1]}] and [{removals[i][0]}-{removals[i][1]}]")

    # Build the new file content
    new_lines = []
    skip_until = -1
    for i, line in enumerate(lines):
        if i <= skip_until:
            continue

        replaced = False
        for start, end, replacement in removals:
            if i == start:
                new_lines.append(replacement)
                skip_until = end
                replaced = True
                break
        if not replaced:
            new_lines.append(line)

    result = '\n'.join(new_lines)

    # Clean up multiple consecutive blank lines
    result = re.sub(r'\n{3,}', '\n\n', result)

    with open(fx_path, 'w', newline='\n') as f:
        f.write(result)

    fname = os.path.basename(fx_path)
    details = []
    if has_shadow:
        s = "shadow"
        if zbias_value or zbias_calc_value:
            s += f"(bias={zbias_value or zbias_calc_value})"
        if is_road_fade:
            s += "(road)"
        details.append(s)
    if has_normal_map:
        details.append("normalmap")
    if not has_irradiance:
        details.append("no-irradiance")
    print(f"  {fname}: {', '.join(details) if details else 'base'}")


# ============================================================================
# Main
# ============================================================================

def main():
    print("=" * 70)
    print("Burnout 5 Shader Preprocessor Reversal")
    print("=" * 70)

    # Phase 0: Backup
    print("\nPhase 0: Creating backup...")
    if not os.path.exists(BACKUP_DIR):
        shutil.copytree(os.path.join(BASE_DIR, "Bundle"), os.path.join(BACKUP_DIR, "Bundle"))
        shutil.copytree(EXECUTABLE_DIR, os.path.join(BACKUP_DIR, "Executable"))
        print("  Backup created at", BACKUP_DIR)
    else:
        print("  Backup already exists, skipping")

    # Phase 1: Clean up PREPROCESSOR_DEFINE_TRUE in Executable files
    print("\nPhase 1: Cleaning PREPROCESSOR_DEFINE_TRUE from Executable files...")
    exe_files = find_all_fx_files(EXECUTABLE_DIR)
    for fx_path in exe_files:
        with open(fx_path, 'r') as f:
            content = f.read()
        content = phase1_cleanup_preprocessor(content)
        # Handle special D_PLATFORM_X360 || PREPROCESSOR_DEFINE_TRUE cases
        if 'PREPROCESSOR_DEFINE_TRUE' in content:
            content = phase1_cleanup_executable_special(content)
        if 'PREPROCESSOR_DEFINE_TRUE' in content:
            print(f"  WARNING: Residual PREPROCESSOR_DEFINE_TRUE in {os.path.basename(fx_path)}")
        with open(fx_path, 'w', newline='\n') as f:
            f.write(content)
    print(f"  Cleaned {len(exe_files)} Executable files")

    # Phase 2: Create header files
    print("\nPhase 2: Creating header files...")
    # Read the canonical file (after Phase 1 cleanup)
    canonical_path = os.path.join(SHADERS_DIR, 'Diffuse_Opaque_Singlesided.fx')
    with open(canonical_path, 'r') as f:
        canonical_content = f.read()
    canonical_content = phase1_cleanup_preprocessor(canonical_content)
    canonical_lines = canonical_content.split('\n')
    create_header_files(canonical_lines)

    # Phase 3: Extract shared code from Bundle files
    print("\nPhase 3: Extracting shared code from Bundle files...")
    bundle_files = find_all_fx_files(os.path.join(BASE_DIR, "Bundle"))
    for fx_path in bundle_files:
        phase3_extract_bundle(fx_path)
    print(f"  Processed {len(bundle_files)} Bundle files")

    # Phase 4: Validation
    print("\nPhase 4: Validation...")
    # Check for any remaining PREPROCESSOR_DEFINE_TRUE references
    all_files = find_all_fx_files(BASE_DIR)
    residual_count = 0
    for fx_path in all_files:
        if '_backup' in fx_path:
            continue
        with open(fx_path, 'r') as f:
            content = f.read()
        if 'PREPROCESSOR_DEFINE_TRUE' in content:
            print(f"  RESIDUAL: {os.path.relpath(fx_path, BASE_DIR)}")
            residual_count += 1
    if residual_count == 0:
        print("  No residual PREPROCESSOR_DEFINE_TRUE references found")
    else:
        print(f"  WARNING: {residual_count} files still contain PREPROCESSOR_DEFINE_TRUE")

    print("\nDone!")


if __name__ == '__main__':
    main()
