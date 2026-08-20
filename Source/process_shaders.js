const fs = require('fs');
const path = require('path');

const BASE_DIR = __dirname;
const BUNDLE_DIR = path.join(BASE_DIR, 'Bundle', 'gamedb', 'burnout5');
const SHADERS_DIR = path.join(BUNDLE_DIR, 'Shaders');
const INCLUDE_DIR = path.join(BUNDLE_DIR, 'Include');
const EXECUTABLE_DIR = path.join(BASE_DIR, 'Executable');
const BACKUP_DIR = path.join(BASE_DIR, '_backup');

const PREPROCESSOR_HEADER =
  '// These defines were added by the ShaderPreProcessor tool to allow it to work\n' +
  '#define PREPROCESSOR_DEFINE_TRUE\n' +
  '// End of Added Defines\n';

function findAllFxFiles(dir) {
  const result = [];
  function walk(d) {
    for (const entry of fs.readdirSync(d, { withFileTypes: true })) {
      const full = path.join(d, entry.name);
      if (entry.isDirectory()) walk(full);
      else if (entry.name.endsWith('.fx')) result.push(full);
    }
  }
  walk(dir);
  return result.sort();
}

function copyDirSync(src, dest) {
  fs.mkdirSync(dest, { recursive: true });
  for (const entry of fs.readdirSync(src, { withFileTypes: true })) {
    const s = path.join(src, entry.name);
    const d = path.join(dest, entry.name);
    if (entry.isDirectory()) copyDirSync(s, d);
    else fs.copyFileSync(s, d);
  }
}

function findFunctionEnd(lines, startIdx) {
  let depth = 0;
  let foundOpen = false;
  for (let i = startIdx; i < lines.length; i++) {
    for (const ch of lines[i]) {
      if (ch === '{') { depth++; foundOpen = true; }
      else if (ch === '}') {
        depth--;
        if (foundOpen && depth === 0) return i;
      }
    }
  }
  return startIdx;
}

function findBlockByRegexAndFuncEnd(lines, startRe, endFuncName) {
  let startIdx = null;
  for (let i = 0; i < lines.length; i++) {
    if (startRe.test(lines[i].trim())) { startIdx = i; break; }
  }
  if (startIdx === null) return [null, null];

  let endIdx = null;
  for (let i = startIdx; i < lines.length; i++) {
    if (lines[i].includes(endFuncName)) {
      // For functions with return type on previous line, start finding braces from current line
      endIdx = findFunctionEnd(lines, i);
      break;
    }
  }
  return [startIdx, endIdx];
}


function findShadowBlock(lines) {
  const startRe = /^float4x4\s+ShadowMap_WorldToLight\[3\]\s*$/;
  let startIdx = null;
  for (let i = 0; i < lines.length; i++) {
    if (startRe.test(lines[i].trim())) { startIdx = i; break; }
  }
  if (startIdx === null) return [null, null];

  const endFuncs = [
    'CalcShadowFactorCSM_Vehicle_Damaged_2CSM_Select',
    'CalcShadowFactor1CSM',
    'CalcShadowFactor2CSMSelect',
    'CalcShadowFactor2CSM',
    'CalcShadowFactor3CSM',
  ];

  let lastFuncEnd = null;
  for (const funcName of endFuncs) {
    for (let i = startIdx; i < lines.length; i++) {
      const stripped = lines[i].trim();
      if (stripped.includes(funcName)) {
        // Check this line or previous line for return type
        const prevLine = i > 0 ? lines[i - 1].trim() : '';
        if (stripped.includes('float') || stripped.includes('void') ||
            prevLine === 'float' || prevLine === 'void') {
          const candidate = findFunctionEnd(lines, i);
          if (lastFuncEnd === null || candidate > lastFuncEnd) {
            lastFuncEnd = candidate;
          }
        }
      }
    }
  }
  return [startIdx, lastFuncEnd];
}

function findDepthEncodeBlock(lines) {
  for (let i = 0; i < lines.length; i++) {
    const stripped = lines[i].trim();
    if (/^#if(?:def)?\b.*D_MRT/.test(stripped) && i + 1 < lines.length && lines[i + 1].includes('ConvertDepthToARGB')) {
      let depth = 1;
      for (let j = i + 1; j < lines.length; j++) {
        const s = lines[j].trim();
        if (s.startsWith('#if')) depth++;
        else if (s === '#endif') {
          depth--;
          if (depth === 0) return [i, j];
        }
      }
    }
  }
  return [null, null];
}

function findNormalMapBlock(lines) {
  let startIdx = null;
  for (let i = 0; i < lines.length; i++) {
    if (lines[i].includes('DecodeNormalMap(')) {
      if (i > 0 && lines[i - 1].trim().startsWith('float3')) {
        startIdx = i - 1;
      } else {
        startIdx = i;
      }
      break;
    }
  }
  if (startIdx === null) return [null, null];

  let endIdx = null;
  for (let i = startIdx; i < lines.length; i++) {
    if (lines[i].includes('TransformTangetSpaceNormalToWorldSpaceNormal')) {
      endIdx = findFunctionEnd(lines, i);
      break;
    }
  }
  return [startIdx, endIdx];
}

function detectShadowZbias(lines, sStart, sEnd) {
  if (sStart === null) return null;
  const end = sEnd !== null ? sEnd + 1 : lines.length;
  for (let i = sStart; i < end; i++) {
    const m = lines[i].match(/position\d\.z\s*-=\s*([\d.]+)\s*\*\s*ShadowMap_Constants2\.z/);
    if (m) return m[1];
  }
  return null;
}

function detectShadowZbiasCalc(lines, sStart, sEnd) {
  if (sStart === null) return null;
  const end = sEnd !== null ? sEnd + 1 : lines.length;
  let inCalc3 = false;
  for (let i = sStart; i < end; i++) {
    if (lines[i].includes('CalcShadowFactor3CSM') && lines[i].includes('float') && !lines[i].includes('Vehicle_Damaged')) {
      inCalc3 = true;
    }
    if (inCalc3) {
      const m = lines[i].match(/metaMapTexCoord\.z\s*-=\s*([\d.]+)\s*\*\s*ShadowMap_Constants2\.z/);
      if (m) return m[1];
      if (lines[i].trim() === '}') break;
    }
  }
  return null;
}

function detectRoadApplyFade(lines, sStart, sEnd) {
  if (sStart === null) return false;
  const end = sEnd !== null ? sEnd + 1 : lines.length;
  for (let i = sStart; i < end; i++) {
    if (lines[i].includes('ApplyFade') && lines[i].includes('float')) {
      for (let j = i; j < Math.min(i + 5, end); j++) {
        if (lines[j].includes('saturate( factor )') && lines[j].includes('ShadowMap_Constants2.y')) {
          return true;
        }
      }
      break;
    }
  }
  return false;
}

function getIncludePrefix(fxPath) {
  const rel = path.relative(BUNDLE_DIR, fxPath).replace(/\\/g, '/');
  const parts = rel.split('/');
  const depth = parts.length - 1;
  if (depth === 1) return '../Include';
  if (depth === 2) return '../../Include';
  const fileDir = path.dirname(fxPath);
  return path.relative(fileDir, INCLUDE_DIR).replace(/\\/g, '/');
}

// ============================================================================
// Phase 1
// ============================================================================

function phase1CleanupPreprocessor(content) {
  // Normalize line endings to LF for processing
  content = content.replace(/\r\n/g, '\n');

  // Remove the 3-line header block
  content = content.replace(
    /\/\/ These defines were added by the ShaderPreProcessor tool to allow it to work\n#define PREPROCESSOR_DEFINE_TRUE\n\/\/ End of Added Defines\n/,
    ''
  );

  // #if defined(PREPROCESSOR_DEFINE_TRUE) && defined(X) -> #ifdef X
  content = content.replace(
    /#if\s+defined\s*\(\s*PREPROCESSOR_DEFINE_TRUE\s*\)\s*&&\s*defined\s*\(\s*(\w+)\s*\)/g,
    '#ifdef $1'
  );

  // #elif defined(PREPROCESSOR_DEFINE_TRUE) && defined(X) -> #elif defined(X)
  content = content.replace(
    /#elif\s+defined\s*\(\s*PREPROCESSOR_DEFINE_TRUE\s*\)\s*&&\s*defined\s*\(\s*(\w+)\s*\)/g,
    '#elif defined($1)'
  );

  // #if defined(PREPROCESSOR_DEFINE_TRUE) && ( defined(X) || defined(Y) )
  content = content.replace(
    /#if\s+defined\s*\(\s*PREPROCESSOR_DEFINE_TRUE\s*\)\s*&&\s*\(\s*defined\s*\(\s*(\w+)\s*\)\s*\|\|\s*defined\s*\(\s*(\w+)\s*\)\s*\)/g,
    '#if defined($1) || defined($2)'
  );

  // #elif defined(PREPROCESSOR_DEFINE_TRUE) && ( defined(X) || defined(Y) )
  content = content.replace(
    /#elif\s+defined\s*\(\s*PREPROCESSOR_DEFINE_TRUE\s*\)\s*&&\s*\(\s*defined\s*\(\s*(\w+)\s*\)\s*\|\|\s*defined\s*\(\s*(\w+)\s*\)\s*\)/g,
    '#elif defined($1) || defined($2)'
  );

  // Handle negated pattern: #if !defined(PREPROCESSOR_DEFINE_TRUE) && !defined(X)
  // Since PREPROCESSOR_DEFINE_TRUE is always defined, !defined() is always false,
  // so the entire #if block is dead code. Remove it (and keep #else if present).
  content = removeDeadNegatedBlocks(content);

  // Handle D_PLATFORM_X360 || PREPROCESSOR_DEFINE_TRUE (always true -> unconditional)
  content = removeAlwaysTrueBlocks(content);

  return content;
}

function removeDeadNegatedBlocks(content) {
  const lines = content.split('\n');
  const result = [];
  let i = 0;
  const negatedRe = /^#if\s+!defined\s*\(\s*PREPROCESSOR_DEFINE_TRUE\s*\)/;

  while (i < lines.length) {
    if (negatedRe.test(lines[i].trim())) {
      // This block is dead code (condition is always false)
      // Find matching #endif, keep #else branch if present
      let depth = 1;
      const elseLines = [];
      let inElse = false;
      let j = i + 1;
      while (j < lines.length && depth > 0) {
        const s = lines[j].trim();
        if (s.startsWith('#if')) { depth++; if (inElse) elseLines.push(lines[j]); }
        else if (s === '#endif') {
          depth--;
          if (depth === 0) { j++; break; }
          if (inElse) elseLines.push(lines[j]);
        }
        else if ((s.startsWith('#else')) && depth === 1) { inElse = true; }
        else {
          if (inElse) elseLines.push(lines[j]);
        }
        j++;
      }
      // Keep only the else branch (if any)
      result.push(...elseLines);
      i = j;
    } else {
      result.push(lines[i]);
      i++;
    }
  }
  return result.join('\n');
}

function removeAlwaysTrueBlocks(content) {
  const lines = content.split('\n');
  const result = [];
  let i = 0;
  const alwaysTrueRe = /^#if\s+defined\s*\(?\s*D_PLATFORM_X360\s*\)?\s*\|\|\s*defined\s*\(?\s*PREPROCESSOR_DEFINE_TRUE\s*\)?/;

  while (i < lines.length) {
    if (alwaysTrueRe.test(lines[i].trim())) {
      let depth = 1;
      const blockLines = [];
      let inElse = false;
      let j = i + 1;
      while (j < lines.length && depth > 0) {
        const s = lines[j].trim();
        if (s.startsWith('#if')) { depth++; if (!inElse) blockLines.push(lines[j]); }
        else if (s === '#endif') {
          depth--;
          if (depth === 0) { j++; break; }
          if (!inElse) blockLines.push(lines[j]);
        }
        else if ((s.startsWith('#else') || s.startsWith('#elif')) && depth === 1) { inElse = true; }
        else { if (!inElse) blockLines.push(lines[j]); }
        j++;
      }
      result.push(...blockLines);
      i = j;
    } else {
      result.push(lines[i]);
      i++;
    }
  }
  return result.join('\n');
}

// phase1CleanupExecutableSpecial is now integrated into phase1CleanupPreprocessor

// ============================================================================
// Phase 2
// ============================================================================

function writeHeader(filename, content) {
  const guard = filename.toUpperCase().replace('.', '_');
  const filePath = path.join(INCLUDE_DIR, filename);
  if (filename === 'Shadow.fxh') {
    fs.writeFileSync(filePath, content);
  } else {
    fs.writeFileSync(filePath,
      `#ifndef ${guard}\n#define ${guard}\n\n${content}\n#endif\n`
    );
  }
  console.log(`  Created ${filename}`);
}

function createHeaderFiles(canonicalLines) {
  fs.mkdirSync(INCLUDE_DIR, { recursive: true });

  // Transform.fxh
  const [tStart, tEnd] = findBlockByRegexAndFuncEnd(canonicalLines,
    /^float4x4\s+viewProjection\s*:\s*ViewProjection\s*$/, 'GetViewSpaceDepthFromWorldPosition');
  writeHeader('Transform.fxh', canonicalLines.slice(tStart, tEnd + 1).join('\n') + '\n');

  // Shadow.fxh
  const [sStart, sEnd] = findShadowBlock(canonicalLines);
  let shadowLines = canonicalLines.slice(sStart, sEnd + 1);
  let shadowContent = shadowLines.join('\n');

  // Build parameterized shadow header
  let shadowOut = [];
  shadowOut.push('#ifndef SHADOW_FXH');
  shadowOut.push('#define SHADOW_FXH');
  shadowOut.push('');

  const sLines = shadowContent.split('\n');
  let currentFunc = null;
  let funcBraceDepth = 0;
  let funcBraceStarted = false;

  for (let idx = 0; idx < sLines.length; idx++) {
    const line = sLines[idx];
    const stripped = line.trim();

    // Detect entering a GetShadowMapPositions function
    const getFuncs = [
      'GetShadowMapPositions2CSMSelect(',
      'GetShadowMapPositions2CSMSelectVS(',
      'GetShadowMapPositions3CSM_4(',
      'GetShadowMapPositions3CSM(',
      'GetShadowMapPositions2CSM(',
      'GetShadowMapPositions1CSM(',
    ];
    for (const gf of getFuncs) {
      if (stripped.includes(gf)) {
        const prevLine = idx > 0 ? sLines[idx - 1].trim() : '';
        if (prevLine === 'void' || stripped.startsWith('void')) {
          currentFunc = gf.replace('(', '');
          funcBraceDepth = 0;
          funcBraceStarted = false;
        }
      }
    }

    // Detect CalcShadowFactor3CSM function
    if (stripped.includes('CalcShadowFactor3CSM(') && !stripped.includes('Vehicle_Damaged') && !stripped.includes('X360')) {
      const prevLine = idx > 0 ? sLines[idx - 1].trim() : '';
      if (prevLine === 'float' || stripped.startsWith('float')) {
        currentFunc = 'CalcShadowFactor3CSM';
        funcBraceDepth = 0;
        funcBraceStarted = false;
      }
    }

    // Track braces
    if (currentFunc) {
      for (const ch of stripped) {
        if (ch === '{') { funcBraceDepth++; funcBraceStarted = true; }
        else if (ch === '}') { funcBraceDepth--; }
      }
    }

    shadowOut.push(line);

    // Insert z-bias after position assignment lines in GetShadowMapPositions functions
    if (currentFunc && funcBraceStarted && funcBraceDepth > 0) {
      if (currentFunc === 'GetShadowMapPositions3CSM' && /^\s*float3\s+position0\s*=\s*mul\(/.test(stripped)) {
        shadowOut.push('#ifdef SHADOW_APPLY_Z_BIAS');
        shadowOut.push('    position0.z -= SHADOW_Z_BIAS_VALUE * ShadowMap_Constants2.z;');
        shadowOut.push('#endif');
      } else if ((currentFunc === 'GetShadowMapPositions2CSMSelectVS' ||
                  currentFunc === 'GetShadowMapPositions2CSM' ||
                  currentFunc === 'GetShadowMapPositions2CSMSelect') &&
                 /^\s*float3\s+position1\s*=\s*mul\(/.test(stripped)) {
        shadowOut.push('#ifdef SHADOW_APPLY_Z_BIAS');
        shadowOut.push('    position0.z -= SHADOW_Z_BIAS_VALUE * ShadowMap_Constants2.z;');
        shadowOut.push('    position1.z -= SHADOW_Z_BIAS_VALUE * ShadowMap_Constants2.z;');
        shadowOut.push('#endif');
      } else if (currentFunc === 'GetShadowMapPositions1CSM' && /^\s*float3\s+position0\s*=\s*mul\(/.test(stripped)) {
        shadowOut.push('#ifdef SHADOW_APPLY_Z_BIAS');
        shadowOut.push('    position0.z -= SHADOW_Z_BIAS_VALUE * ShadowMap_Constants2.z;');
        shadowOut.push('#endif');
      } else if (currentFunc === 'CalcShadowFactor3CSM' && /^\s*float3\s+metaMapTexCoord\s*=/.test(stripped)) {
        shadowOut.push('#ifdef SHADOW_APPLY_Z_BIAS');
        shadowOut.push('    metaMapTexCoord.z -= SHADOW_Z_BIAS_VALUE * ShadowMap_Constants2.z;');
        shadowOut.push('#endif');
      }
    }

    // Handle end of function
    if (currentFunc && funcBraceStarted && funcBraceDepth === 0) {
      currentFunc = null;
    }
  }

  // Now handle ApplyFade parameterization
  const finalShadow = [];
  for (const line of shadowOut) {
    if (line.trim().includes('return factor * fadeValue + (float)1 - fadeValue;')) {
      finalShadow.push('#ifdef SHADOW_APPLY_FADE_ROAD');
      finalShadow.push('    return saturate( factor ) * fadeValue + ( (float)1 - fadeValue ) * (float)ShadowMap_Constants2.y; ');
      finalShadow.push('#else');
      finalShadow.push(line);
      finalShadow.push('#endif');
    } else {
      finalShadow.push(line);
    }
  }
  finalShadow.push('');
  finalShadow.push('#endif');
  finalShadow.push('');

  fs.writeFileSync(path.join(INCLUDE_DIR, 'Shadow.fxh'), finalShadow.join('\n'));
  console.log('  Created Shadow.fxh');

  // Fog.fxh
  const [fStart, fEnd] = findBlockByRegexAndFuncEnd(canonicalLines,
    /^float4\s+ScattCoeffs\s*$/, 'CalculateScattering');
  writeHeader('Fog.fxh', canonicalLines.slice(fStart, fEnd + 1).join('\n') + '\n');

  // Irradiance.fxh
  const [irStart, irEnd] = findBlockByRegexAndFuncEnd(canonicalLines,
    /^float4x4\s+IrradianceQuadricA\s*$/, 'ComputeIrradianceFast');
  writeHeader('Irradiance.fxh', canonicalLines.slice(irStart, irEnd + 1).join('\n') + '\n');

  // DepthEncode.fxh
  const [deStart, deEnd] = findDepthEncodeBlock(canonicalLines);
  writeHeader('DepthEncode.fxh', canonicalLines.slice(deStart, deEnd + 1).join('\n') + '\n');

  // NormalMapping.fxh (from Diffuse_1Bit_Doublesided.fx)
  const nmFile = path.join(SHADERS_DIR, 'Diffuse_1Bit_Doublesided.fx');
  let nmContent = fs.readFileSync(nmFile, 'utf-8');
  nmContent = phase1CleanupPreprocessor(nmContent);
  const nmLines = nmContent.split('\n');
  const [nmStart, nmEnd] = findNormalMapBlock(nmLines);
  if (nmStart !== null && nmEnd !== null) {
    writeHeader('NormalMapping.fxh', nmLines.slice(nmStart, nmEnd + 1).join('\n') + '\n');
  }
}

// ============================================================================
// Phase 3
// ============================================================================

function phase3ExtractBundle(fxPath) {
  let content = fs.readFileSync(fxPath, 'utf-8');
  content = phase1CleanupPreprocessor(content);
  const lines = content.split('\n');
  const includePrefix = getIncludePrefix(fxPath);

  const hasNormalMap = lines.some(l => l.includes('DecodeNormalMap('));
  const hasShadow = lines.some(l => /^float4x4\s+ShadowMap_WorldToLight\[3\]\s*$/.test(l.trim()));
  const hasIrradiance = lines.some(l => /^float4x4\s+IrradianceQuadricA\s*$/.test(l.trim()));

  let zbiasValue = null;
  let zbiasCalcValue = null;
  let isRoadFade = false;

  if (hasShadow) {
    const [ss, se] = findShadowBlock(lines);
    zbiasValue = detectShadowZbias(lines, ss, se);
    zbiasCalcValue = detectShadowZbiasCalc(lines, ss, se);
    isRoadFade = detectRoadApplyFade(lines, ss, se);
  }

  const removals = [];

  // Normal mapping
  if (hasNormalMap) {
    const [nmS, nmE] = findNormalMapBlock(lines);
    if (nmS !== null && nmE !== null) {
      removals.push([nmS, nmE, `#include "${includePrefix}/NormalMapping.fxh"`]);
    }
  }

  // Transform
  const [tS, tE] = findBlockByRegexAndFuncEnd(lines,
    /^float4x4\s+viewProjection\s*:\s*ViewProjection\s*$/, 'GetViewSpaceDepthFromWorldPosition');
  if (tS !== null && tE !== null) {
    removals.push([tS, tE, `#include "${includePrefix}/Transform.fxh"`]);
  }

  // Shadow
  if (hasShadow) {
    const [sS, sE] = findShadowBlock(lines);
    if (sS !== null && sE !== null) {
      let shadowDefines = '';
      const bias = zbiasValue || zbiasCalcValue;
      if (bias) {
        shadowDefines = `#define SHADOW_APPLY_Z_BIAS\n#define SHADOW_Z_BIAS_VALUE ${bias}\n`;
      }
      if (isRoadFade) {
        shadowDefines += '#define SHADOW_APPLY_FADE_ROAD\n';
      }
      removals.push([sS, sE, shadowDefines + `#include "${includePrefix}/Shadow.fxh"`]);
    }
  }

  // Fog
  const [fS, fE] = findBlockByRegexAndFuncEnd(lines,
    /^float4\s+ScattCoeffs\s*$/, 'CalculateScattering');
  if (fS !== null && fE !== null) {
    removals.push([fS, fE, `#include "${includePrefix}/Fog.fxh"`]);
  }

  // Irradiance
  if (hasIrradiance) {
    const [irS, irE] = findBlockByRegexAndFuncEnd(lines,
      /^float4x4\s+IrradianceQuadricA\s*$/, 'ComputeIrradianceFast');
    if (irS !== null && irE !== null) {
      removals.push([irS, irE, `#include "${includePrefix}/Irradiance.fxh"`]);
    }
  }

  // DepthEncode
  const [deS, deE] = findDepthEncodeBlock(lines);
  if (deS !== null && deE !== null) {
    removals.push([deS, deE, `#include "${includePrefix}/DepthEncode.fxh"`]);
  }

  // Sort by start line
  removals.sort((a, b) => a[0] - b[0]);

  // Validate no overlaps
  for (let i = 1; i < removals.length; i++) {
    if (removals[i][0] <= removals[i - 1][1]) {
      console.log(`  WARNING: Overlapping blocks in ${path.basename(fxPath)}: ` +
        `[${removals[i - 1][0]}-${removals[i - 1][1]}] and [${removals[i][0]}-${removals[i][1]}]`);
    }
  }

  // Build new content
  const newLines = [];
  let skipUntil = -1;
  for (let i = 0; i < lines.length; i++) {
    if (i <= skipUntil) continue;
    let replaced = false;
    for (const [start, end, replacement] of removals) {
      if (i === start) {
        newLines.push(replacement);
        skipUntil = end;
        replaced = true;
        break;
      }
    }
    if (!replaced) newLines.push(lines[i]);
  }

  let result = newLines.join('\n');
  result = result.replace(/\n{3,}/g, '\n\n');

  fs.writeFileSync(fxPath, result);

  const fname = path.basename(fxPath);
  const details = [];
  if (hasShadow) {
    let s = 'shadow';
    const bias = zbiasValue || zbiasCalcValue;
    if (bias) s += `(bias=${bias})`;
    if (isRoadFade) s += '(road)';
    details.push(s);
  }
  if (hasNormalMap) details.push('normalmap');
  if (!hasIrradiance) details.push('no-irradiance');
  console.log(`  ${fname}: ${details.length ? details.join(', ') : 'base'}`);
}

// ============================================================================
// Main
// ============================================================================

function main() {
  console.log('='.repeat(70));
  console.log('Burnout 5 Shader Preprocessor Reversal');
  console.log('='.repeat(70));

  // Phase 0: Backup
  console.log('\nPhase 0: Creating backup...');
  if (!fs.existsSync(BACKUP_DIR)) {
    copyDirSync(path.join(BASE_DIR, 'Bundle'), path.join(BACKUP_DIR, 'Bundle'));
    copyDirSync(EXECUTABLE_DIR, path.join(BACKUP_DIR, 'Executable'));
    console.log('  Backup created at', BACKUP_DIR);
  } else {
    console.log('  Backup already exists, skipping');
  }

  // Phase 1: Executable files
  console.log('\nPhase 1: Cleaning PREPROCESSOR_DEFINE_TRUE from Executable files...');
  const exeFiles = findAllFxFiles(EXECUTABLE_DIR);
  for (const fxPath of exeFiles) {
    let content = fs.readFileSync(fxPath, 'utf-8');
    content = phase1CleanupPreprocessor(content);
    if (content.includes('PREPROCESSOR_DEFINE_TRUE')) {
      console.log(`  WARNING: Residual PREPROCESSOR_DEFINE_TRUE in ${path.basename(fxPath)}`);
    }
    fs.writeFileSync(fxPath, content);
  }
  console.log(`  Cleaned ${exeFiles.length} Executable files`);

  // Phase 2: Create header files
  console.log('\nPhase 2: Creating header files...');
  const canonicalPath = path.join(SHADERS_DIR, 'Diffuse_Opaque_Singlesided.fx');
  let canonicalContent = fs.readFileSync(canonicalPath, 'utf-8');
  canonicalContent = phase1CleanupPreprocessor(canonicalContent);
  const canonicalLines = canonicalContent.split('\n');
  createHeaderFiles(canonicalLines);

  // Phase 3: Extract shared code from Bundle files
  console.log('\nPhase 3: Extracting shared code from Bundle files...');
  const bundleFiles = findAllFxFiles(path.join(BASE_DIR, 'Bundle'));
  for (const fxPath of bundleFiles) {
    phase3ExtractBundle(fxPath);
  }
  console.log(`  Processed ${bundleFiles.length} Bundle files`);

  // Phase 4: Validation
  console.log('\nPhase 4: Validation...');
  const allFiles = findAllFxFiles(BASE_DIR);
  let residualCount = 0;
  for (const fxPath of allFiles) {
    if (fxPath.includes('_backup')) continue;
    const content = fs.readFileSync(fxPath, 'utf-8');
    if (content.includes('PREPROCESSOR_DEFINE_TRUE')) {
      console.log(`  RESIDUAL: ${path.relative(BASE_DIR, fxPath)}`);
      residualCount++;
    }
  }
  if (residualCount === 0) {
    console.log('  No residual PREPROCESSOR_DEFINE_TRUE references found');
  } else {
    console.log(`  WARNING: ${residualCount} files still contain PREPROCESSOR_DEFINE_TRUE`);
  }

  console.log('\nDone!');
}

main();
