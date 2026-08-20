const fs = require('fs');
const path = require('path');

const BASE_DIR = __dirname;
const BUNDLE_DIR = path.join(BASE_DIR, 'Bundle', 'gamedb', 'burnout5');
const INCLUDE_DIR = path.join(BUNDLE_DIR, 'Include');
const SHADERS_DIR = path.join(BUNDLE_DIR, 'Shaders');
const SKINTEST_DIR = path.join(BUNDLE_DIR, 'Playground', 'SkinTest');

function findFunctionEnd(lines, startIdx) {
  let depth = 0, foundOpen = false;
  for (let i = startIdx; i < lines.length; i++) {
    for (const ch of lines[i]) {
      if (ch === '{') { depth++; foundOpen = true; }
      else if (ch === '}') { depth--; if (foundOpen && depth === 0) return i; }
    }
  }
  return startIdx;
}

function findFuncBlock(lines, funcName) {
  for (let i = 0; i < lines.length; i++) {
    if (lines[i].includes(funcName + '(') || lines[i].includes(funcName + ' (')) {
      // Check if this is a function DEFINITION (not a call)
      // Look at previous line for return type
      const prevLine = i > 0 ? lines[i - 1].trim() : '';
      const curLine = lines[i].trim();
      const isDefinition = prevLine === 'float3' || prevLine === 'float4' || prevLine === 'float' || prevLine === 'void' ||
                           curLine.startsWith('float3') || curLine.startsWith('float4') || curLine.startsWith('float ') || curLine.startsWith('void');
      if (isDefinition) {
        const start = (prevLine === 'float3' || prevLine === 'float4' || prevLine === 'float' || prevLine === 'void') ? i - 1 : i;
        const end = findFunctionEnd(lines, i);
        return [start, end];
      }
    }
  }
  return [null, null];
}

function getIncludePrefix(fxPath) {
  const rel = path.relative(BUNDLE_DIR, fxPath).replace(/\\/g, '/');
  const depth = rel.split('/').length - 1;
  if (depth === 1) return '../Include';
  return '../../Include';
}

// ============================================================================
// Create VehicleCommon.fxh
// ============================================================================

function createVehicleCommonHeader() {
  const canonical = path.join(SHADERS_DIR, 'Vehicle_Opaque_Metal_Textured.fx');
  const content = fs.readFileSync(canonical, 'utf-8').replace(/\r\n/g, '\n');
  const lines = content.split('\n');

  const [voStart, voEnd] = findFuncBlock(lines, 'VerletOffset');
  const [vosStart, vosEnd] = findFuncBlock(lines, 'VerletOffsetPlusScratches');
  const [lsStart, lsEnd] = findFuncBlock(lines, 'LinearStep1Fast');
  const [acStart, acEnd] = findFuncBlock(lines, 'AdjustContrast');
  const [asStart, asEnd] = findFuncBlock(lines, 'AdjustSaturation');
  const [cfStart, cfEnd] = findFuncBlock(lines, 'CalculateFresnel');
  const [dnmStart, dnmEnd] = findFuncBlock(lines, 'DecodeNormalMap');

  let header = '#ifndef VEHICLECOMMON_FXH\n#define VEHICLECOMMON_FXH\n\n';

  // VerletOffset
  header += lines.slice(voStart, voEnd + 1).join('\n') + '\n\n';

  // VerletOffsetPlusScratches (conditional)
  header += '#ifdef VEHICLE_USE_SCRATCHES\n';
  header += lines.slice(vosStart, vosEnd + 1).join('\n') + '\n';
  header += '#endif\n\n';

  // LinearStep1Fast
  header += lines.slice(lsStart, lsEnd + 1).join('\n') + '\n\n';

  // AdjustContrast (parameterized)
  header += 'float3\nAdjustContrast(\n    float3   colour,\n    float    contrast )\n{\n';
  header += '#ifdef CONTRAST_USE_FIXED_MID\n';
  header += '    return ( ( ( colour - 0.5 ) * contrast ) + 0.5 );\n';
  header += '#else\n';
  header += '    float lfHalfWhiteLevel = FogColourPlusWhiteLevel.w * 0.5;\n';
  header += '    return ( ( ( colour - lfHalfWhiteLevel ) * contrast ) + lfHalfWhiteLevel );\n';
  header += '#endif\n}\n\n';

  // AdjustSaturation
  header += lines.slice(asStart, asEnd + 1).join('\n') + '\n\n';

  // CalculateFresnel (parameterized)
  header += 'void\nCalculateFresnel(\n';
  header += '    float3       normal,\n';
  header += '    float3       view,\n';
  header += '    float4       fresnelRanges,\n';
  header += '    float        fresnelCurve,\n';
  header += '    out float3   reflectedView,\n';
  header += '    out float2   fresnelValues )\n{\n';
  header += ' float normalDotView = dot( normal, view );\n';
  header += '#ifdef FRESNEL_USE_CURVE\n';
  header += '    float    baseFresnel = pow( 1.0 - saturate( normalDotView ), fresnelCurve );\n';
  header += '#else\n';
  header += '    float    baseFresnel = 1.0 - saturate( normalDotView );\n';
  header += '#endif\n';
  header += '    reflectedView = 2.0 * normalDotView * normal - view;\n';
  header += '    fresnelValues = lerp( fresnelRanges.xz, fresnelRanges.yw, baseFresnel );\n';
  header += '}\n\n';

  // DecodeNormalMap (Vehicle variant)
  header += lines.slice(dnmStart, dnmEnd + 1).join('\n') + '\n\n';

  header += '#endif\n';

  fs.writeFileSync(path.join(INCLUDE_DIR, 'VehicleCommon.fxh'), header);
  console.log('Created VehicleCommon.fxh');
}

// ============================================================================
// Create Skinning.fxh
// ============================================================================

function createSkinningHeader() {
  const canonical = path.join(SKINTEST_DIR, 'Vehicle_Opaque_Metal_Textured_Skin.fx');
  const content = fs.readFileSync(canonical, 'utf-8').replace(/\r\n/g, '\n');
  const lines = content.split('\n');

  const [dpStart, dpEnd] = findFuncBlock(lines, 'DoSkinningP');
  const [dpnStart, dpnEnd] = findFuncBlock(lines, 'DoSkinningPN');
  const [dpntStart, dpntEnd] = findFuncBlock(lines, 'DoSkinningPNT');

  let header = '#ifndef SKINNING_FXH\n#define SKINNING_FXH\n\n';
  header += lines.slice(dpStart, dpEnd + 1).join('\n') + '\n';
  header += lines.slice(dpnStart, dpnEnd + 1).join('\n') + '\n';
  header += lines.slice(dpntStart, dpntEnd + 1).join('\n') + '\n';
  header += '\n#endif\n';

  fs.writeFileSync(path.join(INCLUDE_DIR, 'Skinning.fxh'), header);
  console.log('Created Skinning.fxh');
}

// ============================================================================
// Extract from Vehicle files
// ============================================================================

function extractFromVehicleFile(fxPath) {
  let content = fs.readFileSync(fxPath, 'utf-8').replace(/\r\n/g, '\n');
  const lines = content.split('\n');
  const fname = path.basename(fxPath);
  const prefix = getIncludePrefix(fxPath);

  // Check which functions exist
  const hasVerletOffset = lines.some(l => l.includes('VerletOffset(') && (l.trim().startsWith('float3') || (lines[Math.max(0, lines.indexOf(l) - 1)] || '').trim() === 'float3'));
  const hasVerletOffsetPS = lines.some(l => l.includes('VerletOffsetPlusScratches(') && (l.trim().startsWith('float4') || (lines[Math.max(0, lines.indexOf(l) - 1)] || '').trim() === 'float4'));
  const hasLinearStep = lines.some(l => l.includes('LinearStep1Fast(') && ((lines[Math.max(0, lines.indexOf(l) - 1)] || '').trim() === 'float' || l.trim().startsWith('float')));
  const hasAdjustContrast = lines.some(l => l.includes('AdjustContrast(') && ((lines[Math.max(0, lines.indexOf(l) - 1)] || '').trim() === 'float3' || l.trim().startsWith('float3')));
  const hasAdjustSaturation = lines.some(l => l.includes('AdjustSaturation(') && ((lines[Math.max(0, lines.indexOf(l) - 1)] || '').trim() === 'float3' || l.trim().startsWith('float3')));
  const hasCalculateFresnel = lines.some(l => l.includes('CalculateFresnel(') && ((lines[Math.max(0, lines.indexOf(l) - 1)] || '').trim() === 'void' || l.trim().startsWith('void')));

  // Check for Vehicle DecodeNormalMap (not the NormalMapping.fxh one)
  let hasVehicleDNM = false;
  for (let i = 0; i < lines.length; i++) {
    if (lines[i].includes('DecodeNormalMap(')) {
      const prev = i > 0 ? lines[i - 1].trim() : '';
      if (prev === 'float3' || lines[i].trim().startsWith('float3')) {
        // Check it's the vehicle variant (uses tangent - binormal, not tangent + binormal)
        const end = findFunctionEnd(lines, i);
        const body = lines.slice(i, end + 1).join('\n');
        if (body.includes('tangent ) - ( result.x')) {
          hasVehicleDNM = true;
        }
        break;
      }
    }
  }

  if (!hasVerletOffset && !hasLinearStep && !hasAdjustContrast && !hasCalculateFresnel && !hasVehicleDNM) {
    return false; // Nothing to extract
  }

  // Detect variants
  const isPowFresnel = content.includes('pow( 1.0 - saturate( normalDotView ), fresnelCurve )');
  const isFixedMidContrast = lines.some(l => l.includes('colour - 0.5 ) * contrast') && l.includes('AdjustContrast') === false);
  // Better check for fixed mid: look in AdjustContrast function body
  let isFixedContrast = false;
  const [acS, acE] = findFuncBlock(lines, 'AdjustContrast');
  if (acS !== null) {
    const acBody = lines.slice(acS, acE + 1).join('\n');
    isFixedContrast = acBody.includes('colour - 0.5 )') && !acBody.includes('FogColourPlusWhiteLevel');
  }

  // Collect removal ranges
  const removals = [];
  const funcsToRemove = [];

  if (hasVerletOffset) funcsToRemove.push('VerletOffset');
  if (hasVerletOffsetPS) funcsToRemove.push('VerletOffsetPlusScratches');
  if (hasLinearStep) funcsToRemove.push('LinearStep1Fast');
  if (hasAdjustContrast) funcsToRemove.push('AdjustContrast');
  if (hasAdjustSaturation) funcsToRemove.push('AdjustSaturation');
  if (hasCalculateFresnel) funcsToRemove.push('CalculateFresnel');
  if (hasVehicleDNM) funcsToRemove.push('DecodeNormalMap');

  for (const funcName of funcsToRemove) {
    const [start, end] = findFuncBlock(lines, funcName);
    if (start !== null && end !== null) {
      removals.push([start, end]);
    }
  }

  // Sort and merge overlapping ranges
  removals.sort((a, b) => a[0] - b[0]);

  // Build defines
  let defines = '';
  if (isPowFresnel) defines += '#define FRESNEL_USE_CURVE\n';
  if (isFixedContrast) defines += '#define CONTRAST_USE_FIXED_MID\n';
  if (hasVerletOffsetPS) defines += '#define VEHICLE_USE_SCRATCHES\n';

  // Find where to insert the #include — after g_verletOffsets declaration or after last existing include
  let insertLine = -1;
  for (let i = 0; i < lines.length; i++) {
    if (lines[i].includes('g_verletOffsets[')) {
      // Find end of this declaration (look for ">;" which closes annotation block)
      for (let j = i; j < lines.length; j++) {
        if (lines[j].trim() === '>;' || lines[j].trim().startsWith('> =') || lines[j].trim().startsWith('>=')) {
          insertLine = j;
          break;
        }
      }
      break;
    }
  }

  // If no g_verletOffsets found, insert right before the first function definition
  // (after the last constant/uniform/sampler declaration)
  if (insertLine === -1) {
    // Find the first VerletOffset or LinearStep1Fast or CalculateFresnel definition
    for (let i = 0; i < lines.length; i++) {
      const stripped = lines[i].trim();
      if (stripped === 'float3' || stripped === 'float4' || stripped === 'float' || stripped === 'void') {
        const nextLine = i + 1 < lines.length ? lines[i + 1].trim() : '';
        if (nextLine.startsWith('VerletOffset(') || nextLine.startsWith('LinearStep1Fast(') ||
            nextLine.startsWith('AdjustContrast(') || nextLine.startsWith('CalculateFresnel(') ||
            nextLine.startsWith('DecodeNormalMap(')) {
          insertLine = i - 1; // Insert before the return type line
          break;
        }
      }
    }
  }

  // Fallback: insert after the last #include
  if (insertLine === -1) {
    for (let i = lines.length - 1; i >= 0; i--) {
      if (lines[i].trim().startsWith('#include')) { insertLine = i; break; }
    }
  }

  // Build new content
  const newLines = [];
  let skipUntil = -1;

  for (let i = 0; i < lines.length; i++) {
    if (i <= skipUntil) continue;

    let removed = false;
    for (const [start, end] of removals) {
      if (i === start) {
        skipUntil = end;
        removed = true;
        break;
      }
    }
    if (removed) continue;

    newLines.push(lines[i]);

    // Insert include after the insertion point
    if (i === insertLine) {
      newLines.push(defines + `#include "${prefix}/VehicleCommon.fxh"`);
    }
  }

  let result = newLines.join('\n');
  result = result.replace(/\n{3,}/g, '\n\n');

  fs.writeFileSync(fxPath, result);

  const details = [];
  if (isPowFresnel) details.push('pow-fresnel');
  if (isFixedContrast) details.push('fixed-contrast');
  if (hasVerletOffsetPS) details.push('scratches');
  if (hasVehicleDNM) details.push('vehicle-dnm');
  console.log(`  ${fname}: ${details.length ? details.join(', ') : 'standard'}`);
  return true;
}

// ============================================================================
// Extract from SkinTest files
// ============================================================================

function extractFromSkinTestFile(fxPath) {
  let content = fs.readFileSync(fxPath, 'utf-8').replace(/\r\n/g, '\n');
  const lines = content.split('\n');
  const fname = path.basename(fxPath);
  const prefix = getIncludePrefix(fxPath);

  const funcsToRemove = ['DoSkinningP', 'DoSkinningPN', 'DoSkinningPNT'];
  const removals = [];

  for (const funcName of funcsToRemove) {
    const [start, end] = findFuncBlock(lines, funcName);
    if (start !== null && end !== null) {
      removals.push([start, end]);
    }
  }

  if (removals.length === 0) return;

  removals.sort((a, b) => a[0] - b[0]);

  // Insert after boneMatrices declaration
  let insertLine = -1;
  for (let i = 0; i < lines.length; i++) {
    if (lines[i].includes('boneMatrices[')) {
      for (let j = i; j < lines.length; j++) {
        if (lines[j].includes(';') || lines[j].includes('>')) { insertLine = j; break; }
      }
      break;
    }
  }

  const newLines = [];
  let skipUntil = -1;

  for (let i = 0; i < lines.length; i++) {
    if (i <= skipUntil) continue;

    let removed = false;
    for (const [start, end] of removals) {
      if (i === start) { skipUntil = end; removed = true; break; }
    }
    if (removed) continue;

    newLines.push(lines[i]);
    if (i === insertLine) {
      newLines.push(`#include "${prefix}/Skinning.fxh"`);
    }
  }

  let result = newLines.join('\n');
  result = result.replace(/\n{3,}/g, '\n\n');

  fs.writeFileSync(fxPath, result);
  console.log(`  ${fname}: skinning extracted`);
}

// ============================================================================
// Main
// ============================================================================

function main() {
  console.log('='.repeat(60));
  console.log('Vehicle & Skinning Extraction');
  console.log('='.repeat(60));

  // Create headers
  console.log('\nCreating headers...');
  createVehicleCommonHeader();
  createSkinningHeader();

  // Find all Vehicle .fx files
  console.log('\nExtracting from Vehicle files...');
  const allFiles = fs.readdirSync(SHADERS_DIR)
    .filter(f => f.startsWith('Vehicle_') && f.endsWith('.fx'))
    .map(f => path.join(SHADERS_DIR, f));

  let extracted = 0;
  for (const f of allFiles) {
    if (extractFromVehicleFile(f)) extracted++;
  }
  console.log(`  Extracted from ${extracted} Vehicle files`);

  // Also process SkinTest files for VehicleCommon
  console.log('\nExtracting VehicleCommon from SkinTest files...');
  if (fs.existsSync(SKINTEST_DIR)) {
    const skinFiles = fs.readdirSync(SKINTEST_DIR)
      .filter(f => f.endsWith('.fx'))
      .map(f => path.join(SKINTEST_DIR, f));
    for (const f of skinFiles) {
      extractFromVehicleFile(f);
    }
  }

  // Extract skinning
  console.log('\nExtracting Skinning from SkinTest files...');
  if (fs.existsSync(SKINTEST_DIR)) {
    const skinFiles = fs.readdirSync(SKINTEST_DIR)
      .filter(f => f.endsWith('.fx'))
      .map(f => path.join(SKINTEST_DIR, f));
    for (const f of skinFiles) {
      extractFromSkinTestFile(f);
    }
  }

  // Validation
  console.log('\nValidation...');
  let residual = 0;
  for (const f of allFiles) {
    const content = fs.readFileSync(f, 'utf-8');
    for (const fn of ['CalculateFresnel', 'LinearStep1Fast', 'AdjustSaturation']) {
      // Check for function definitions (not calls)
      const lines = content.split('\n');
      for (let i = 0; i < lines.length; i++) {
        if (lines[i].includes(fn + '(')) {
          const prev = i > 0 ? lines[i - 1].trim() : '';
          if (prev === 'float' || prev === 'float3' || prev === 'void' ||
              lines[i].trim().startsWith('float ') || lines[i].trim().startsWith('float3') || lines[i].trim().startsWith('void')) {
            console.log(`  RESIDUAL ${fn} definition in ${path.basename(f)}`);
            residual++;
          }
        }
      }
    }
  }
  if (residual === 0) console.log('  No residual function definitions found');

  console.log('\nDone!');
}

main();
