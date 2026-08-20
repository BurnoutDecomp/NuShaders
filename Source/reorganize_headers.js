const fs = require('fs');
const path = require('path');

const BASE_DIR = __dirname;
const BUNDLE_DIR = path.join(BASE_DIR, 'Bundle', 'gamedb', 'burnout5');
const INCLUDE_DIR = path.join(BUNDLE_DIR, 'Include');
const SHADERS_DIR = path.join(BUNDLE_DIR, 'Shaders');
const SKINTEST_DIR = path.join(BUNDLE_DIR, 'Playground', 'SkinTest');

function processFile(fxPath) {
  let content = fs.readFileSync(fxPath, 'utf-8').replace(/\r\n/g, '\n');
  const fname = path.basename(fxPath);

  if (!content.includes('VehicleCommon.fxh')) return false;

  const rel = path.relative(BUNDLE_DIR, fxPath).replace(/\\/g, '/');
  const depth = rel.split('/').length - 1;
  const prefix = depth === 1 ? '../Include' : '../../Include';

  // Detect which defines exist before VehicleCommon include
  const hasFresnelCurve = content.includes('#define FRESNEL_USE_CURVE');
  const hasFixedContrast = content.includes('#define CONTRAST_USE_FIXED_MID');
  const hasScratches = content.includes('#define VEHICLE_USE_SCRATCHES');

  // Build the replacement block
  // NormalMapping with SWAP_TB (Vehicle convention)
  let replacement = `#define NORMAL_MAP_SWAP_TB\n#include "${prefix}/NormalMapping.fxh"\n`;

  // Fresnel (with optional curve define before it)
  if (hasFresnelCurve) {
    replacement += `#define FRESNEL_USE_CURVE\n`;
  }
  replacement += `#include "${prefix}/Fresnel.fxh"\n`;

  // Utility (with optional fixed contrast before it)
  if (hasFixedContrast) {
    replacement += `#define CONTRAST_USE_FIXED_MID\n`;
  }
  replacement += `#include "${prefix}/Utility.fxh"\n`;

  // VehicleDeformation (with optional scratches before it)
  if (hasScratches) {
    replacement += `#define VEHICLE_USE_SCRATCHES\n`;
  }
  replacement += `#include "${prefix}/VehicleDeformation.fxh"`;

  // Remove the old defines that precede VehicleCommon include
  // They could appear on separate lines before the include
  content = content.replace(/#define FRESNEL_USE_CURVE\n/g, '');
  content = content.replace(/#define CONTRAST_USE_FIXED_MID\n/g, '');
  content = content.replace(/#define VEHICLE_USE_SCRATCHES\n/g, '');

  // Replace VehicleCommon include with new block
  const includePattern = new RegExp(`#include "${prefix.replace(/\//g, '\\/')}/VehicleCommon\\.fxh"`);
  content = content.replace(includePattern, replacement);

  // Clean up multiple blank lines
  content = content.replace(/\n{3,}/g, '\n\n');

  fs.writeFileSync(fxPath, content);

  const details = [];
  if (hasFresnelCurve) details.push('fresnel-curve');
  if (hasFixedContrast) details.push('fixed-contrast');
  if (hasScratches) details.push('scratches');
  console.log(`  ${fname}: ${details.length ? details.join(', ') : 'standard'}`);
  return true;
}

console.log('Reorganizing headers...');
console.log('\nUpdating Vehicle shaders:');

let count = 0;
for (const f of fs.readdirSync(SHADERS_DIR).filter(f => f.endsWith('.fx')).sort()) {
  if (processFile(path.join(SHADERS_DIR, f))) count++;
}

console.log('\nUpdating SkinTest shaders:');
if (fs.existsSync(SKINTEST_DIR)) {
  for (const f of fs.readdirSync(SKINTEST_DIR).filter(f => f.endsWith('.fx')).sort()) {
    if (processFile(path.join(SKINTEST_DIR, f))) count++;
  }
}

console.log(`\nUpdated ${count} files`);

// Delete old VehicleCommon.fxh
const oldHeader = path.join(INCLUDE_DIR, 'VehicleCommon.fxh');
if (fs.existsSync(oldHeader)) {
  fs.unlinkSync(oldHeader);
  console.log('Deleted VehicleCommon.fxh');
}

// Validation
console.log('\nValidation:');
function findAllFx(dir) {
  const r = [];
  function walk(d) {
    for (const e of fs.readdirSync(d, {withFileTypes:true})) {
      const f = path.join(d, e.name);
      if (e.isDirectory()) walk(f);
      else if (e.name.endsWith('.fx') || e.name.endsWith('.fxh')) r.push(f);
    }
  }
  walk(dir);
  return r;
}

let residual = 0;
for (const f of findAllFx(path.join(BASE_DIR, 'Bundle'))) {
  const c = fs.readFileSync(f, 'utf-8');
  if (c.includes('VehicleCommon')) {
    console.log(`  RESIDUAL VehicleCommon reference: ${path.relative(BASE_DIR, f)}`);
    residual++;
  }
}
if (residual === 0) console.log('  No residual VehicleCommon references');

// Count NORMAL_MAP_SWAP_TB usage
let swapCount = 0;
for (const f of findAllFx(path.join(BASE_DIR, 'Bundle'))) {
  if (fs.readFileSync(f, 'utf-8').includes('NORMAL_MAP_SWAP_TB')) swapCount++;
}
console.log(`  NORMAL_MAP_SWAP_TB used in ${swapCount} files`);

console.log('\nDone!');
