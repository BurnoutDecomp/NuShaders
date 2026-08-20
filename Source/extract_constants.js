const fs = require('fs');
const path = require('path');

const BASE_DIR = __dirname;
const BUNDLE_DIR = path.join(BASE_DIR, 'Bundle', 'gamedb', 'burnout5');
const INCLUDE_DIR = path.join(BUNDLE_DIR, 'Include');

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

function getIncludePrefix(fxPath) {
  const rel = path.relative(BUNDLE_DIR, fxPath).replace(/\\/g, '/');
  const depth = rel.split('/').length - 1;
  if (depth === 1) return '../Include';
  if (depth === 2) return '../../Include';
  return path.relative(path.dirname(fxPath), INCLUDE_DIR).replace(/\\/g, '/');
}

// Create Constants.fxh
const constantsHeader = `#ifndef CONSTANTS_FXH
#define CONSTANTS_FXH

static const float3 k_luminanceMapping = float3( 0.299, 0.587, 0.114 );

#endif
`;

fs.writeFileSync(path.join(INCLUDE_DIR, 'Constants.fxh'), constantsHeader);
console.log('Created Constants.fxh');

// Process all Bundle .fx files
const bundleFiles = findAllFxFiles(path.join(BASE_DIR, 'Bundle'));
let extractedKLum = 0;
let extractedHDR = 0;
let fixedCamelCase = 0;

for (const fxPath of bundleFiles) {
  let content = fs.readFileSync(fxPath, 'utf-8');
  content = content.replace(/\r\n/g, '\n');
  let changed = false;

  const fname = path.basename(fxPath);
  const prefix = getIncludePrefix(fxPath);

  // Extract k_luminanceMapping
  if (content.includes('static const float3 k_luminanceMapping = float3( 0.299, 0.587, 0.114 );')) {
    content = content.replace(
      'static const float3 k_luminanceMapping = float3( 0.299, 0.587, 0.114 );\n',
      `#include "${prefix}/Constants.fxh"\n`
    );
    extractedKLum++;
    changed = true;
  }

  // Handle kLuminanceMapping variant (camelCase, no underscore)
  if (content.includes('static const float3 kLuminanceMapping = float3( 0.299, 0.587, 0.114 );')) {
    content = content.replace(
      'static const float3 kLuminanceMapping = float3( 0.299, 0.587, 0.114 );\n',
      `#include "${prefix}/Constants.fxh"\n`
    );
    // Also rename usages from kLuminanceMapping to k_luminanceMapping
    content = content.replace(/\bkLuminanceMapping\b/g, 'k_luminanceMapping');
    fixedCamelCase++;
    extractedKLum++;
    changed = true;
  }

  // Extract HDRConstants uniform (only if standalone, not mixed with other content)
  // HDRConstants appears as a 4-line block right after k_luminanceMapping
  const hdrBlock = 'float4 HDRConstants\n<\n string scope = "global";\n>;\n';
  if (content.includes(hdrBlock)) {
    // Check if Constants.fxh include already present (from k_luminanceMapping extraction)
    if (content.includes(`#include "${prefix}/Constants.fxh"`)) {
      // Just remove the HDRConstants block; we'll add it to Constants.fxh
      content = content.replace(hdrBlock, '');
    } else {
      // Replace with include
      content = content.replace(hdrBlock, `#include "${prefix}/Constants.fxh"\n`);
    }
    extractedHDR++;
    changed = true;
  }

  if (changed) {
    // Clean up multiple blank lines
    content = content.replace(/\n{3,}/g, '\n\n');
    fs.writeFileSync(fxPath, content);
    console.log(`  ${fname}: extracted`);
  }
}

// Update Constants.fxh to include HDRConstants if any were extracted
if (extractedHDR > 0) {
  const updatedHeader = `#ifndef CONSTANTS_FXH
#define CONSTANTS_FXH

static const float3 k_luminanceMapping = float3( 0.299, 0.587, 0.114 );

#ifdef SHADER_USE_HDR
float4 HDRConstants
<
 string scope = "global";
>;
#endif

#endif
`;
  // Actually, HDRConstants should always be available when the header is included,
  // since not all files that include the header use HDR. But having an unused uniform
  // declared is fine in HLSL — the compiler ignores it. So just include it unconditionally.
  const simpleHeader = `#ifndef CONSTANTS_FXH
#define CONSTANTS_FXH

static const float3 k_luminanceMapping = float3( 0.299, 0.587, 0.114 );

float4 HDRConstants
<
 string scope = "global";
>;

#endif
`;
  fs.writeFileSync(path.join(INCLUDE_DIR, 'Constants.fxh'), simpleHeader);
  console.log('Updated Constants.fxh with HDRConstants');
}

console.log(`\nSummary:`);
console.log(`  k_luminanceMapping extracted: ${extractedKLum} files`);
console.log(`  HDRConstants extracted: ${extractedHDR} files`);
console.log(`  kLuminanceMapping -> k_luminanceMapping renames: ${fixedCamelCase} files`);
