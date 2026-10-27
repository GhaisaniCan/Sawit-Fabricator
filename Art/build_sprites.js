// Usage (from Art/): npm install @resvg/resvg-js@2.6.2, then node build_sprites.js [name ...]
// Generates the Corrupting Tech vs. Botanical Seed sprites: writes standalone SVGs to Art/Source and
// renders each to a 512x512 PNG in Assets/Sprites/Cells (overwriting in place so prefab GUIDs survive).
const fs = require('fs');
const path = require('path');
const { Resvg } = require('@resvg/resvg-js');

const ROOT = path.resolve(__dirname, '..');
// PREVIEW=dir renders into that dir only, leaving the project untouched
const PREVIEW = process.env.PREVIEW;
const SRC = PREVIEW || path.join(ROOT, 'Art/Source');
const OUT = PREVIEW || path.join(ROOT, 'Assets/Sprites/Cells');
const only = process.argv.slice(2);

const svg = (comment, defs, body) =>
`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256" width="256" height="256">
  <!-- ${comment} -->
  <defs>${defs}
  </defs>${body}
</svg>
`;

// ---------- shared: seed matrix background (pastel, low contrast; it's the backdrop) ----------
const matrixDefs = `
    <linearGradient id="mx" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#f4ead4"/>
      <stop offset="1" stop-color="#e9dbbb"/>
    </linearGradient>
    <radialGradient id="mxCell" cx="45%" cy="40%" r="70%">
      <stop offset="0" stop-color="#fbf5e6" stop-opacity="0.75"/>
      <stop offset="1" stop-color="#f1e4c6" stop-opacity="0.2"/>
    </radialGradient>`;
const matrixBody = `
  <rect width="256" height="256" fill="url(#mx)"/>
  <!-- seed-pod cells (kept off the tile edges so tiles join cleanly) -->
  <g fill="url(#mxCell)" stroke="#d9c6a0" stroke-width="3" stroke-opacity="0.55" stroke-linejoin="round">
    <path d="M22,24 L116,20 L122,102 L66,116 L20,94 Z"/>
    <path d="M136,20 L234,24 L236,108 L172,120 L132,98 Z"/>
    <path d="M20,112 L68,130 L112,124 L118,234 L24,236 Z"/>
    <path d="M130,116 L176,134 L236,126 L234,234 L134,236 Z"/>
  </g>
  <g fill="#e6cf9c" opacity="0.45">
    <circle cx="70" cy="64" r="9"/><circle cx="192" cy="186" r="11"/><circle cx="182" cy="66" r="5"/>
  </g>
  <!-- incubation fluid bubbles -->
  <g fill="#ffffff" opacity="0.45">
    <circle cx="96" cy="170" r="5"/><circle cx="108" cy="186" r="2.5"/><circle cx="210" cy="92" r="3.5"/>
    <circle cx="48" cy="200" r="3"/><circle cx="150" cy="56" r="2.5"/>
  </g>
  <!-- faint holographic fabricator grid -->
  <g stroke="#8fcfd0" stroke-width="1.2" opacity="0.18">
    <line x1="64" y1="0" x2="64" y2="256"/><line x1="128" y1="0" x2="128" y2="256"/><line x1="192" y1="0" x2="192" y2="256"/>
    <line x1="0" y1="64" x2="256" y2="64"/><line x1="0" y1="128" x2="256" y2="128"/><line x1="0" y1="192" x2="256" y2="192"/>
  </g>
  <g fill="none" stroke="#7cc9cc" stroke-width="2.5" stroke-linecap="round" opacity="0.4">
    <path d="M10,24 V10 H24"/><path d="M232,10 H246 V24"/><path d="M246,232 V246 H232"/><path d="M24,246 H10 V232"/>
  </g>
  <rect x="3" y="3" width="250" height="250" rx="9" fill="none" stroke="#cdb98f" stroke-width="4" opacity="0.55"/>`;

const sprites = {};

// ---------- tiles ----------
sprites.cytoplasm_tile = svg(
  'Seed Matrix tile (base): pastel amber seed-pod cells over a faint holographic fabricator grid',
  matrixDefs, matrixBody);

sprites.cellulose_wall = svg(
  'Lignin Wall: dense woody seed-coat block over the matrix. Darker than the matrix so it reads as solid',
  matrixDefs + `
    <linearGradient id="wood" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#c4a77a"/>
      <stop offset="0.55" stop-color="#a3855a"/>
      <stop offset="1" stop-color="#7d6342"/>
    </linearGradient>
    <clipPath id="block"><rect x="8" y="8" width="240" height="240" rx="24"/></clipPath>`,
  matrixBody + `
  <rect x="8" y="8" width="240" height="240" rx="24" fill="url(#wood)"/>
  <g clip-path="url(#block)" fill="none" stroke-linecap="round">
    <!-- growth rings around two knots -->
    <g stroke="#6e5536" stroke-width="3" opacity="0.45">
      <ellipse cx="88" cy="92" rx="22" ry="14"/><ellipse cx="88" cy="92" rx="40" ry="26"/><ellipse cx="88" cy="92" rx="60" ry="40"/>
      <ellipse cx="180" cy="176" rx="18" ry="12"/><ellipse cx="180" cy="176" rx="36" ry="24"/><ellipse cx="180" cy="176" rx="58" ry="38"/>
    </g>
    <!-- lignin grain -->
    <g stroke="#e2cda0" stroke-width="3" opacity="0.4">
      <path d="M-10,150 C50,138 90,160 140,146 S230,130 270,140"/>
      <path d="M-10,220 C40,210 100,230 150,214 S230,204 270,212"/>
      <path d="M-10,36 C60,30 120,46 170,34 S240,28 270,36"/>
    </g>
  </g>
  <g fill="#5e4a30"><ellipse cx="88" cy="92" rx="8" ry="5"/><ellipse cx="180" cy="176" rx="7" ry="4.5"/></g>
  <rect x="16" y="16" width="224" height="224" rx="18" fill="none" stroke="#eadbb8" stroke-width="3" opacity="0.45"/>
  <rect x="8" y="8" width="240" height="240" rx="24" fill="none" stroke="#5e4a30" stroke-width="7"/>`);

// ---------- player: Tech Bug ----------
const hex = (r, cx = 128, cy = 128) => {
  const p = [];
  for (let i = 0; i < 6; i++) {
    const a = Math.PI / 6 + i * Math.PI / 3;
    p.push(`${(cx + r * Math.cos(a)).toFixed(1)},${(cy + r * Math.sin(a)).toFixed(1)}`);
  }
  return 'M' + p.join(' L') + ' Z';
};
sprites.cancer_cell = svg(
  'Tech Bug (Cyber-Infection Core): hexagonal nanite hull, neon cyan/magenta circuitry, glitch fringe',
  `
    <linearGradient id="hull" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#3a4660"/>
      <stop offset="0.5" stop-color="#222a3c"/>
      <stop offset="1" stop-color="#121722"/>
    </linearGradient>
    <linearGradient id="plate" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#4b5874"/>
      <stop offset="1" stop-color="#1c2230"/>
    </linearGradient>
    <radialGradient id="core" cx="45%" cy="40%" r="60%">
      <stop offset="0" stop-color="#ffffff"/>
      <stop offset="0.35" stop-color="#ff8fe0"/>
      <stop offset="0.75" stop-color="#ff3dbe"/>
      <stop offset="1" stop-color="#7a0f63"/>
    </radialGradient>
    <filter id="glow" x="-40%" y="-40%" width="180%" height="180%"><feGaussianBlur stdDeviation="7"/></filter>
    <filter id="soft" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="3"/></filter>`,
  `
  <!-- glitch aura: chromatic offset copies of the hull -->
  <path d="${hex(96, 121, 128)}" fill="#22e5f0" opacity="0.45" filter="url(#glow)"/>
  <path d="${hex(96, 135, 128)}" fill="#ff3dbe" opacity="0.45" filter="url(#glow)"/>

  <!-- nanite legs (three per side) -->
  <g stroke="#0a0e16" stroke-width="13" stroke-linecap="round" stroke-linejoin="round" fill="none">
    <path d="M70,92 L40,70 L26,78"/><path d="M58,128 L26,128 L16,138"/><path d="M70,164 L40,186 L28,200"/>
    <path d="M186,92 L216,70 L230,78"/><path d="M198,128 L230,128 L240,138"/><path d="M186,164 L216,186 L228,200"/>
  </g>
  <g stroke="#6f7f9e" stroke-width="6" stroke-linecap="round" stroke-linejoin="round" fill="none">
    <path d="M70,92 L40,70 L26,78"/><path d="M58,128 L26,128 L16,138"/><path d="M70,164 L40,186 L28,200"/>
    <path d="M186,92 L216,70 L230,78"/><path d="M198,128 L230,128 L240,138"/><path d="M186,164 L216,186 L228,200"/>
  </g>
  <g fill="#22e5f0"><circle cx="40" cy="70" r="4"/><circle cx="26" cy="128" r="4"/><circle cx="40" cy="186" r="4"/></g>
  <g fill="#ff3dbe"><circle cx="216" cy="70" r="4"/><circle cx="230" cy="128" r="4"/><circle cx="216" cy="186" r="4"/></g>

  <!-- antennae -->
  <g stroke="#0a0e16" stroke-width="9" stroke-linecap="round" fill="none"><path d="M108,46 L94,22"/><path d="M148,46 L162,22"/></g>
  <g stroke="#8fa0c0" stroke-width="4" stroke-linecap="round" fill="none"><path d="M108,46 L94,22"/><path d="M148,46 L162,22"/></g>
  <circle cx="94" cy="22" r="6" fill="#22e5f0"/><circle cx="162" cy="22" r="6" fill="#ff3dbe"/>

  <!-- hull -->
  <path d="${hex(88)}" fill="url(#hull)" stroke="#0a0e16" stroke-width="7" stroke-linejoin="round"/>
  <!-- plating panels -->
  <path d="${hex(66)}" fill="url(#plate)" stroke="#0a0e16" stroke-width="4" stroke-linejoin="round"/>
  <g stroke="#0a0e16" stroke-width="4">
    <line x1="128" y1="40" x2="128" y2="62"/><line x1="128" y1="194" x2="128" y2="216"/>
    <line x1="52" y1="84" x2="71" y2="95"/><line x1="204" y1="84" x2="185" y2="95"/>
    <line x1="52" y1="172" x2="71" y2="161"/><line x1="204" y1="172" x2="185" y2="161"/>
  </g>
  <path d="M68,92 L128,58 L188,92" fill="none" stroke="#9fb0d4" stroke-width="3" opacity="0.5" stroke-linejoin="round"/>
  <!-- rivets -->
  <g fill="#8fa0c0"><circle cx="128" cy="50" r="3"/><circle cx="62" cy="90" r="3"/><circle cx="194" cy="90" r="3"/>
    <circle cx="62" cy="166" r="3"/><circle cx="194" cy="166" r="3"/><circle cx="128" cy="206" r="3"/></g>

  <!-- circuit traces -->
  <g fill="none" stroke-width="4" stroke-linecap="round" stroke-linejoin="round">
    <path d="M100,128 H82 L74,118" stroke="#22e5f0"/>
    <path d="M112,104 L104,90 H90" stroke="#22e5f0"/>
    <path d="M112,152 L104,166 H92" stroke="#22e5f0"/>
    <path d="M156,128 H174 L182,138" stroke="#ff3dbe"/>
    <path d="M144,104 L152,90 H166" stroke="#ff3dbe"/>
    <path d="M144,152 L152,166 H164" stroke="#ff3dbe"/>
  </g>
  <g fill="#22e5f0"><circle cx="74" cy="118" r="5"/><circle cx="90" cy="90" r="5"/><circle cx="92" cy="166" r="5"/></g>
  <g fill="#ff3dbe"><circle cx="182" cy="138" r="5"/><circle cx="166" cy="90" r="5"/><circle cx="164" cy="166" r="5"/></g>

  <!-- corruption core -->
  <circle cx="128" cy="128" r="32" fill="#ff3dbe" opacity="0.8" filter="url(#glow)"/>
  <path d="${hex(30)}" fill="#0a0e16"/>
  <circle cx="128" cy="128" r="22" fill="url(#core)"/>
  <circle cx="128" cy="128" r="22" fill="none" stroke="#22e5f0" stroke-width="3" stroke-dasharray="8 6"/>
  <circle cx="121" cy="121" r="6" fill="#ffffff" opacity="0.9"/>

  <!-- glitch scanline slices -->
  <g opacity="0.7">
    <rect x="58" y="108" width="34" height="5" fill="#22e5f0"/>
    <rect x="170" y="150" width="30" height="4" fill="#ff3dbe"/>
    <rect x="96" y="196" width="22" height="4" fill="#22e5f0"/>
    <rect x="150" y="58" width="26" height="4" fill="#ff3dbe"/>
  </g>`);

// ---------- target: Seed Embryo ----------
const petals = (n, r1, r2, w, rot = 0) => {
  let s = '';
  for (let i = 0; i < n; i++) {
    const a = rot + i * 360 / n;
    s += `<path transform="rotate(${a} 128 128)" d="M128,${128 - r1} C${128 + w},${128 - (r1 + r2) / 2} ${128 + w * 0.6},${128 - r2 + 6} 128,${128 - r2} C${128 - w * 0.6},${128 - r2 + 6} ${128 - w},${128 - (r1 + r2) / 2} 128,${128 - r1} Z"/>`;
  }
  return s;
};
sprites.core_cell = svg(
  'Seed Embryo (Flora Nucleus): pristine seed germ, golden-green sap heart, leafy membranes, radial symmetry',
  `
    <radialGradient id="aura" cx="50%" cy="50%" r="50%">
      <stop offset="0" stop-color="#fff6c8" stop-opacity="0.9"/>
      <stop offset="0.7" stop-color="#e9f5a8" stop-opacity="0.35"/>
      <stop offset="1" stop-color="#e9f5a8" stop-opacity="0"/>
    </radialGradient>
    <linearGradient id="leaf" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#b9e27e"/>
      <stop offset="1" stop-color="#5e9e3e"/>
    </linearGradient>
    <radialGradient id="seed" cx="45%" cy="40%" r="60%">
      <stop offset="0" stop-color="#fffbe6"/>
      <stop offset="0.7" stop-color="#f3e3a6"/>
      <stop offset="1" stop-color="#c9ad5e"/>
    </radialGradient>
    <radialGradient id="sap" cx="45%" cy="40%" r="60%">
      <stop offset="0" stop-color="#fffbd0"/>
      <stop offset="0.45" stop-color="#f5c542"/>
      <stop offset="1" stop-color="#b9820e"/>
    </radialGradient>
    <filter id="glow" x="-40%" y="-40%" width="180%" height="180%"><feGaussianBlur stdDeviation="8"/></filter>`,
  `
  <circle cx="128" cy="128" r="122" fill="url(#aura)"/>
  <!-- outer leafy membranes -->
  <g fill="url(#leaf)" stroke="#355f22" stroke-width="4" stroke-linejoin="round">${petals(8, 60, 116, 30, 22.5)}</g>
  <g fill="none" stroke="#e2f5b8" stroke-width="2.5" opacity="0.7">${petals(8, 66, 104, 0, 22.5).replace(/ Z"/g, '"')}</g>
  <!-- seed coat layers -->
  <circle cx="128" cy="128" r="72" fill="url(#seed)" stroke="#8a6e2c" stroke-width="5"/>
  <circle cx="128" cy="128" r="60" fill="none" stroke="#ffffff" stroke-width="3" opacity="0.55"/>
  <circle cx="128" cy="128" r="50" fill="#f8eec4" opacity="0.6" stroke="#d8bf72" stroke-width="2.5"/>
  <!-- cotyledon ring -->
  <g fill="#9bd16a" stroke="#4a7d2e" stroke-width="2.5" opacity="0.9">${petals(6, 26, 48, 12)}</g>
  <!-- sap heart -->
  <circle cx="128" cy="128" r="30" fill="#f5c542" opacity="0.85" filter="url(#glow)"/>
  <circle cx="128" cy="128" r="22" fill="url(#sap)" stroke="#8a5e08" stroke-width="3"/>
  <!-- tiny sprout -->
  <path d="M128,136 C128,126 128,122 128,116" stroke="#4a7d2e" stroke-width="3.5" fill="none" stroke-linecap="round"/>
  <path d="M128,118 C120,108 110,112 112,118 C116,122 124,122 128,118 Z" fill="#9bd16a" stroke="#4a7d2e" stroke-width="2"/>
  <path d="M128,118 C136,108 146,112 144,118 C140,122 132,122 128,118 Z" fill="#9bd16a" stroke="#4a7d2e" stroke-width="2"/>
  <circle cx="120" cy="134" r="4" fill="#ffffff" opacity="0.8"/>`);

// ---------- enemy: Root Sentinel ----------
sprites.defensive_cell = svg(
  'Root Sentinel: bark-armoured lignin node with moss and thorny root stubs (mid-tones so a crimson tint still reads)',
  `
    <radialGradient id="bark" cx="42%" cy="38%" r="70%">
      <stop offset="0" stop-color="#9a7350"/>
      <stop offset="0.6" stop-color="#6b4a32"/>
      <stop offset="1" stop-color="#3e2a1c"/>
    </radialGradient>
    <radialGradient id="heart" cx="45%" cy="40%" r="60%">
      <stop offset="0" stop-color="#c9a272"/>
      <stop offset="1" stop-color="#7a5636"/>
    </radialGradient>
    <linearGradient id="moss" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#8fb05a"/>
      <stop offset="1" stop-color="#4f6b3a"/>
    </linearGradient>`,
  `
  <!-- root stubs with thorns -->
  <g stroke="#24170f" stroke-width="5" stroke-linejoin="round" fill="#5a3e2b">
    <path d="M118,40 L128,12 L138,40 Z"/><path d="M216,118 L244,128 L216,138 Z"/>
    <path d="M118,216 L128,244 L138,216 Z"/><path d="M40,118 L12,128 L40,138 Z"/>
    <path d="M182,62 L206,50 L194,74 Z"/><path d="M194,182 L206,206 L182,194 Z"/>
    <path d="M62,194 L50,206 L74,182 Z"/><path d="M74,62 L50,50 L62,74 Z"/>
  </g>
  <g fill="#e8d6b0"><path d="M125,22 L128,12 L131,22 Z"/><path d="M234,125 L244,128 L234,131 Z"/>
    <path d="M125,234 L128,244 L131,234 Z"/><path d="M22,125 L12,128 L22,131 Z"/></g>

  <!-- bark shell -->
  <path d="M128,34 L180,46 L214,82 L222,128 L210,176 L176,210 L128,222 L80,210 L46,176 L34,128 L46,80 L80,46 Z"
        fill="url(#bark)" stroke="#24170f" stroke-width="7" stroke-linejoin="round"/>
  <!-- bark plate cracks -->
  <g fill="none" stroke="#2e1e14" stroke-width="4" stroke-linecap="round" opacity="0.8">
    <path d="M128,40 L120,72"/><path d="M206,92 L176,104"/><path d="M200,168 L172,156"/>
    <path d="M128,216 L134,184"/><path d="M54,166 L84,154"/><path d="M52,96 L82,104"/>
  </g>
  <path d="M60,92 C72,64 96,50 124,46" fill="none" stroke="#b8936a" stroke-width="4" stroke-linecap="round" opacity="0.6"/>
  <!-- moss patches -->
  <g fill="url(#moss)" stroke="#33461f" stroke-width="2.5">
    <path d="M58,104 C54,86 72,72 90,78 C100,68 120,70 118,84 C108,92 96,92 86,104 C76,112 62,114 58,104 Z"/>
    <path d="M168,188 C180,180 198,184 198,170 C206,176 206,196 190,204 C178,210 164,200 168,188 Z"/>
  </g>
  <g fill="#c3db8a" opacity="0.7"><circle cx="74" cy="90" r="4"/><circle cx="98" cy="80" r="3"/><circle cx="188" cy="190" r="3"/></g>
  <!-- leaf sprigs -->
  <g fill="#2f4a2a" stroke="#1c2c18" stroke-width="2">
    <path d="M176,62 C196,54 208,64 204,72 C192,76 182,72 176,62 Z"/>
    <path d="M64,180 C50,196 56,210 64,208 C72,198 72,188 64,180 Z"/>
  </g>
  <!-- heartwood eye -->
  <circle cx="128" cy="128" r="44" fill="url(#heart)" stroke="#24170f" stroke-width="5"/>
  <g fill="none" stroke="#5a3e2b" stroke-width="3" opacity="0.7"><circle cx="128" cy="128" r="32"/><circle cx="128" cy="128" r="20"/></g>
  <circle cx="128" cy="128" r="10" fill="#3e2a1c"/>
  <circle cx="122" cy="120" r="5" fill="#f0dcb4" opacity="0.6"/>`);

// ---------- bramble roots (VineSegment pieces) ----------
const barkDefs = (id, a, b, c) => `
    <linearGradient id="${id}" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="${a}"/>
      <stop offset="0.5" stop-color="${b}"/>
      <stop offset="1" stop-color="${c}"/>
    </linearGradient>`;

sprites.vine_stem = svg(
  `Bramble root stem: HALF segment from the tile centre to the RIGHT edge. The game rotates it toward each
       connected neighbour (and stretches it on diagonals), so two halves meet at every tile edge`,
  barkDefs('bark', '#9a7350', '#6b4a32', '#3e2a1c'),
  `
  <!-- small thorns, kept past x=180 so they clear the knot and crossing stems at intersections -->
  <g fill="#d9c49a" stroke="#24170f" stroke-width="2.5" stroke-linejoin="round">
    <path d="M224,110 L230,98 L236,110 Z"/><path d="M196,146 L202,158 L208,146 Z"/>
  </g>
  <!-- starts slightly left of centre so joints never show a gap -->
  <rect x="112" y="108" width="146" height="40" rx="14" fill="url(#bark)" stroke="#24170f" stroke-width="5"/>
  <!-- bark grain -->
  <g fill="none" stroke="#2e1e14" stroke-width="3" stroke-linecap="round" opacity="0.7">
    <path d="M124,124 C150,118 170,130 196,122 S240,124 258,122"/>
    <path d="M132,138 C160,134 190,142 222,136"/>
  </g>
  <path d="M122,115 H250" stroke="#c4a07a" stroke-width="3" stroke-linecap="round" opacity="0.55"/>
  <!-- leaf -->
  <path d="M190,110 C194,96 206,92 212,96 C208,106 200,111 190,110 Z" fill="#6e8f4a" stroke="#2f4a2a" stroke-width="2.5"/>`);

sprites.vine_stem_thorny = svg(
  `Thorny bramble stem (hazardous): same half-segment shape as vine_stem, in muted red-brown bark with a few short
       hooked thorns. Thorns stay past x=180 so they clear the knot and crossing stems at intersections`,
  barkDefs('bark', '#8a5244', '#5e3328', '#361b17'),
  `
  <!-- hooked thorns, alternating sides -->
  <g fill="#eadcc4" stroke="#2a1210" stroke-width="2.5" stroke-linejoin="round">
    <path d="M184,110 C188,100 194,94 202,92 C198,98 198,104 200,110 Z"/>
    <path d="M226,110 C230,100 236,94 244,92 C240,98 240,104 242,110 Z"/>
    <path d="M204,146 C208,156 214,162 222,164 C218,158 218,152 220,146 Z"/>
  </g>
  <rect x="112" y="108" width="146" height="40" rx="14" fill="url(#bark)" stroke="#2a1210" stroke-width="5"/>
  <g fill="none" stroke="#24100c" stroke-width="3" stroke-linecap="round" opacity="0.6">
    <path d="M124,124 C150,118 170,130 196,122 S240,124 258,122"/>
    <path d="M132,138 C160,134 190,142 222,136"/>
  </g>
  <path d="M122,115 H250" stroke="#c99080" stroke-width="3" stroke-linecap="round" opacity="0.45"/>`);

sprites.vine_knot = svg(
  'Bramble knot: gnarled bark joint drawn at the tile centre where a root bends or branches',
  `
    <radialGradient id="knot" cx="42%" cy="38%" r="65%">
      <stop offset="0" stop-color="#a47c56"/>
      <stop offset="0.6" stop-color="#6b4a32"/>
      <stop offset="1" stop-color="#3e2a1c"/>
    </radialGradient>`,
  `
  <circle cx="128" cy="128" r="36" fill="url(#knot)" stroke="#24170f" stroke-width="5"/>
  <g fill="none" stroke="#2e1e14" stroke-width="3" opacity="0.7"><circle cx="128" cy="128" r="24"/><circle cx="128" cy="128" r="13"/></g>
  <path d="M100,112 A32,32 0 0 1 128,96" fill="none" stroke="#c4a07a" stroke-width="3" opacity="0.6"/>
  <path d="M138,98 C150,86 164,90 162,98 C154,104 144,104 138,98 Z" fill="#6e8f4a" stroke="#2f4a2a" stroke-width="2"/>
  <circle cx="128" cy="128" r="5" fill="#24170f"/>`);

sprites.vine_tip = svg(
  `Bramble tip: the end of a root, a whip-like tendril. Sits at the tile centre and points RIGHT, away from the
       stem it grows from: starts at the stem's full width in bark and tapers straight along the centreline into young
       green growth, so many tips on one board read as calm, consistent ends`,
  `
    <linearGradient id="whip" gradientUnits="userSpaceOnUse" x1="112" y1="0" x2="224" y2="0">
      <stop offset="0" stop-color="#6b4a32"/>
      <stop offset="0.35" stop-color="#6b4a32"/>
      <stop offset="0.7" stop-color="#5f8a3e"/>
      <stop offset="1" stop-color="#9ccc62"/>
    </linearGradient>
    <linearGradient id="shade" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#ffffff" stop-opacity="0.25"/>
      <stop offset="0.45" stop-color="#ffffff" stop-opacity="0"/>
      <stop offset="1" stop-color="#000000" stop-opacity="0.35"/>
    </linearGradient>
    <linearGradient id="leaf" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#b9e27e"/>
      <stop offset="1" stop-color="#4f7a32"/>
    </linearGradient>`,
  `
  <!-- two small thorns, one per side -->
  <g fill="#d9c49a" stroke="#24170f" stroke-width="2.5" stroke-linejoin="round">
    <path d="M166,108 L174,96 L180,110 Z"/><path d="M184,144 L191,156 L196,141 Z"/>
  </g>
  <!-- small leaf near the base -->
  <path d="M140,104 C140,90 150,82 162,82 C162,94 154,102 140,104 Z" fill="url(#leaf)" stroke="#2f4a2a" stroke-width="3" stroke-linejoin="round"/>
  <!-- the whip: full stem width at the base, tapering straight to a point on the centreline -->
  <path d="M118,103 C152,102 188,110 224,128 C188,146 152,154 118,153 C102,153 102,103 118,103 Z"
        fill="url(#whip)" stroke="#24170f" stroke-width="5" stroke-linejoin="round"/>
  <path d="M118,103 C152,102 188,110 224,128 C188,146 152,154 118,153 C102,153 102,103 118,103 Z"
        fill="url(#shade)"/>
  <!-- bark rings near the base, fading out where the growth turns green -->
  <g fill="none" stroke="#2e1e14" stroke-width="3" stroke-linecap="round">
    <path d="M130,105 C125,120 125,136 130,151" opacity="0.6"/>
    <path d="M152,103 C147,117 147,133 152,148" opacity="0.45"/>
    <path d="M174,106 C170,116 170,126 174,136" opacity="0.25"/>
  </g>
  <!-- highlight running along the top to the tip -->
  <path d="M114,111 C150,109 180,113 206,121" fill="none" stroke="#f2f8d8" stroke-width="4"
        stroke-linecap="round" opacity="0.55"/>`);

// ---------- seed-coat gate ----------
sprites.gate_open = svg(
  'Seed-coat Gate, OPEN: cellulose frame with guard-cell lips parted like a stoma, matrix visible through it',
  matrixDefs + `
    <linearGradient id="coat" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#dcbf88"/>
      <stop offset="1" stop-color="#9c7b45"/>
    </linearGradient>
    <linearGradient id="lip" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0" stop-color="#b5d88a"/>
      <stop offset="1" stop-color="#6e9a4a"/>
    </linearGradient>`,
  matrixBody + `
  <!-- seed-coat frame -->
  <path fill="url(#coat)" stroke="#5a4325" stroke-width="5" fill-rule="evenodd"
        d="M10,10 H246 V246 H10 Z M40,40 V216 H216 V40 Z"/>
  <g fill="#5a4325"><circle cx="25" cy="25" r="5"/><circle cx="231" cy="25" r="5"/><circle cx="25" cy="231" r="5"/><circle cx="231" cy="231" r="5"/></g>
  <!-- guard cells, parted -->
  <path d="M128,52 C82,60 58,96 58,128 C58,160 82,196 128,204 C104,186 92,158 92,128 C92,98 104,70 128,52 Z"
        fill="url(#lip)" stroke="#3e5e2a" stroke-width="4" stroke-linejoin="round"/>
  <path d="M128,52 C174,60 198,96 198,128 C198,160 174,196 128,204 C152,186 164,158 164,128 C164,98 152,70 128,52 Z"
        fill="url(#lip)" stroke="#3e5e2a" stroke-width="4" stroke-linejoin="round"/>
  <g fill="#e6f5c8" opacity="0.6"><ellipse cx="74" cy="118" rx="5" ry="14"/><ellipse cx="182" cy="118" rx="5" ry="14"/></g>
  <!-- open pore glow -->
  <ellipse cx="128" cy="128" rx="30" ry="70" fill="none" stroke="#fffbe8" stroke-width="3" opacity="0.7"/>`);

const iris = () => {
  let s = '';
  for (let i = 0; i < 6; i++) {
    s += `<path transform="rotate(${i * 60} 128 128)" d="M128,128 L128,38 C160,40 188,56 204,82 Z"/>`;
  }
  return s;
};
sprites.gate_closed = svg(
  'Seed-coat Gate, CLOSED: same frame, sealed by overlapping cellulose petals like a closed seed coat',
  `
    <linearGradient id="coat" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#dcbf88"/>
      <stop offset="1" stop-color="#9c7b45"/>
    </linearGradient>
    <radialGradient id="petal" cx="50%" cy="50%" r="55%">
      <stop offset="0" stop-color="#8a6a3a"/>
      <stop offset="1" stop-color="#c9a66b"/>
    </radialGradient>`,
  `
  <rect x="10" y="10" width="236" height="236" fill="url(#coat)" stroke="#5a4325" stroke-width="5"/>
  <circle cx="128" cy="128" r="94" fill="#7a5c32" stroke="#5a4325" stroke-width="4"/>
  <g fill="url(#petal)" stroke="#4a3520" stroke-width="4" stroke-linejoin="round">${iris()}</g>
  <g fill="none" stroke="#f0dcb0" stroke-width="2.5" opacity="0.5">${iris().replace(/d="M128,128 L128,38 C160,40 188,56 204,82 Z"/g, 'd="M134,52 C158,56 176,68 188,86"')}</g>
  <circle cx="128" cy="128" r="16" fill="#5a4325" stroke="#3a2a16" stroke-width="4"/>
  <circle cx="123" cy="123" r="4" fill="#f0dcb0" opacity="0.6"/>
  <g fill="#5a4325"><circle cx="25" cy="25" r="5"/><circle cx="231" cy="25" r="5"/><circle cx="25" cy="231" r="5"/><circle cx="231" cy="231" r="5"/></g>`);

// ---------- sap router ----------
sprites.valve_node = svg(
  'Sap Router base: round wooden vascular node on the seed matrix. The glowing sap glyph is a separate, rotatable sprite',
  matrixDefs + `
    <radialGradient id="node" cx="45%" cy="40%" r="60%">
      <stop offset="0" stop-color="#a47c56"/>
      <stop offset="0.65" stop-color="#7a5634"/>
      <stop offset="1" stop-color="#4e3520"/>
    </radialGradient>
    <radialGradient id="pool" cx="50%" cy="45%" r="55%">
      <stop offset="0" stop-color="#7a4f1c"/>
      <stop offset="1" stop-color="#4a2e10"/>
    </radialGradient>`,
  matrixBody + `
  <!-- vascular strands feeding the node -->
  <g stroke="#b48e5e" stroke-width="14" stroke-linecap="round" opacity="0.7">
    <line x1="128" y1="10" x2="128" y2="246"/><line x1="10" y1="128" x2="246" y2="128"/>
  </g>
  <g stroke="#f2d9a0" stroke-width="3" stroke-linecap="round" opacity="0.6">
    <line x1="124" y1="12" x2="124" y2="244"/><line x1="12" y1="124" x2="244" y2="124"/>
  </g>
  <circle cx="128" cy="128" r="88" fill="url(#node)" stroke="#33220f" stroke-width="6"/>
  <g fill="none" stroke="#33220f" stroke-width="2.5" opacity="0.45"><circle cx="128" cy="128" r="78"/><circle cx="128" cy="128" r="68"/></g>
  <!-- ring of vascular pits -->
  <g fill="#f2c14e" stroke="#33220f" stroke-width="2">
    <circle cx="128" cy="54" r="5"/><circle cx="180" cy="76" r="5"/><circle cx="202" cy="128" r="5"/><circle cx="180" cy="180" r="5"/>
    <circle cx="128" cy="202" r="5"/><circle cx="76" cy="180" r="5"/><circle cx="54" cy="128" r="5"/><circle cx="76" cy="76" r="5"/>
  </g>
  <circle cx="128" cy="128" r="54" fill="url(#pool)" stroke="#33220f" stroke-width="4"/>
  <path d="M92,104 A44,44 0 0 1 128,82" fill="none" stroke="#c99a5a" stroke-width="3" opacity="0.6"/>`);

sprites.valve_arrow = svg(
  'Sap Router glyph: glowing sap arrow pointing RIGHT (0°); the game rotates it',
  `
    <linearGradient id="sap" x1="0" y1="0" x2="1" y2="0">
      <stop offset="0" stop-color="#fff2b8"/>
      <stop offset="1" stop-color="#f2c14e"/>
    </linearGradient>
    <filter id="glow" x="-30%" y="-30%" width="160%" height="160%"><feGaussianBlur stdDeviation="8"/></filter>`,
  `
  <path filter="url(#glow)" fill="#ffd966" opacity="0.9" d="M44,104 C80,100 110,104 136,100 V62 L218,128 L136,194 V156 C110,152 80,156 44,152 Z"/>
  <path fill="url(#sap)" stroke="#7a4b00" stroke-width="7" stroke-linejoin="round"
        d="M48,108 C82,104 110,108 138,104 V68 L212,128 L138,188 V152 C110,148 82,152 48,148 C40,138 40,118 48,108 Z"/>
  <path fill="none" stroke="#ffffff" stroke-width="5" stroke-linecap="round" stroke-linejoin="round" opacity="0.75"
        d="M58,118 C90,114 120,118 148,114 V90 L192,126"/>
  <g fill="#fff8d8" opacity="0.85"><circle cx="76" cy="136" r="4"/><circle cx="110" cy="134" r="3"/></g>`);

// ---------- injector gland ----------
const glandDefs = matrixDefs + `
    <radialGradient id="tissue" cx="50%" cy="50%" r="50%">
      <stop offset="0.6" stop-color="#c8dfa0"/>
      <stop offset="1" stop-color="#c8dfa0" stop-opacity="0"/>
    </radialGradient>
    <linearGradient id="steel" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#c2cad4"/>
      <stop offset="0.5" stop-color="#8b95a3"/>
      <stop offset="1" stop-color="#5c6672"/>
    </linearGradient>
    <linearGradient id="steelDark" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#6b7684"/>
      <stop offset="1" stop-color="#2f3640"/>
    </linearGradient>
    <filter id="glow" x="-40%" y="-40%" width="180%" height="180%"><feGaussianBlur stdDeviation="6"/></filter>`;
const octagon = r => {
  const p = [];
  for (let i = 0; i < 8; i++) {
    const a = Math.PI / 8 + i * Math.PI / 4;
    p.push(`${(128 + r * Math.cos(a)).toFixed(1)},${(128 + r * Math.sin(a)).toFixed(1)}`);
  }
  return 'M' + p.join(' L') + ' Z';
};
const glandMount = `
  <!-- swollen plant tissue around the implant -->
  <circle cx="128" cy="128" r="112" fill="url(#tissue)" opacity="0.8"/>
  <!-- bolted steel flange -->
  <path d="${octagon(92)}" fill="url(#steel)" stroke="#262c34" stroke-width="6" stroke-linejoin="round"/>
  <path d="${octagon(80)}" fill="none" stroke="#e4eaf0" stroke-width="2.5" opacity="0.5" stroke-linejoin="round"/>
  <g fill="#3a424c" stroke="#1c2128" stroke-width="2">
    <circle cx="128" cy="52" r="6"/><circle cx="204" cy="128" r="6"/><circle cx="128" cy="204" r="6"/><circle cx="52" cy="128" r="6"/>
  </g>
  <!-- hazard ticks -->
  <g stroke="#f2b84b" stroke-width="5" stroke-linecap="round">
    <line x1="174" y1="70" x2="184" y2="60"/><line x1="186" y1="186" x2="176" y2="176"/>
    <line x1="70" y1="186" x2="80" y2="176"/><line x1="72" y1="72" x2="82" y2="82"/>
  </g>`;

sprites.chemical_valve_idle = svg(
  'Injector Gland, IDLE: Fabricator nozzle bolted into the seed tissue, iris shut, status ring dim',
  glandDefs,
  matrixBody + glandMount + `
  <!-- status ring (dim) -->
  <circle cx="128" cy="128" r="58" fill="url(#steelDark)" stroke="#1c2128" stroke-width="5"/>
  <circle cx="128" cy="128" r="50" fill="none" stroke="#3fe0e8" stroke-width="5" opacity="0.35" stroke-dasharray="14 8"/>
  <!-- closed iris nozzle -->
  <circle cx="128" cy="128" r="38" fill="#4a535e" stroke="#1c2128" stroke-width="4"/>
  <g stroke="#1c2128" stroke-width="3" fill="none">
    <path d="M128,90 L140,128"/><path d="M161,109 L128,128"/><path d="M161,147 L116,128"/>
    <path d="M128,166 L116,128"/><path d="M95,147 L128,128"/><path d="M95,109 L140,128"/>
  </g>
  <circle cx="128" cy="128" r="6" fill="#1c2128"/>
  <path d="M100,104 A36,36 0 0 1 128,92" fill="none" stroke="#c2cad4" stroke-width="3" opacity="0.6"/>`);

sprites.chemical_valve_released = svg(
  'Injector Gland, RELEASED: nozzle iris open, status ring lit, synthetic ingredient spraying into the matrix',
  glandDefs + `
    <radialGradient id="spray" cx="50%" cy="50%" r="50%">
      <stop offset="0" stop-color="#e8feff" stop-opacity="0.95"/>
      <stop offset="0.5" stop-color="#7ff0f5" stop-opacity="0.5"/>
      <stop offset="1" stop-color="#7ff0f5" stop-opacity="0"/>
    </radialGradient>`,
  matrixBody + glandMount + `
  <circle cx="128" cy="128" r="58" fill="url(#steelDark)" stroke="#1c2128" stroke-width="5"/>
  <circle cx="128" cy="128" r="54" fill="none" stroke="#3fe0e8" stroke-width="6" filter="url(#glow)"/>
  <circle cx="128" cy="128" r="50" fill="none" stroke="#a8f8fb" stroke-width="5" stroke-dasharray="14 8"/>
  <!-- open nozzle with fluid -->
  <circle cx="128" cy="128" r="38" fill="#1c2128"/>
  <circle cx="128" cy="128" r="30" fill="#3fe0e8" filter="url(#glow)"/>
  <circle cx="128" cy="128" r="24" fill="#bdfbff"/>
  <circle cx="128" cy="128" r="12" fill="#ffffff"/>
  <!-- spray cloud and droplets -->
  <circle cx="128" cy="128" r="120" fill="url(#spray)" opacity="0.55"/>
  <g fill="#bdfbff" stroke="#1d8f96" stroke-width="2">
    <circle cx="60" cy="60" r="7"/><circle cx="198" cy="58" r="6"/><circle cx="202" cy="198" r="7"/><circle cx="56" cy="196" r="6"/>
    <rect x="124" y="20" width="8" height="8" transform="rotate(45 128 24)"/><rect x="226" y="124" width="8" height="8" transform="rotate(45 230 128)"/>
    <rect x="20" y="124" width="8" height="8" transform="rotate(45 24 128)"/><rect x="124" y="226" width="8" height="8" transform="rotate(45 128 230)"/>
  </g>`);

// ---------- move arrow (tinted by UnitManager, so mostly white) ----------
sprites.move_arrow = svg(
  `Move Arrow (8-way slide selection), pointing RIGHT; the game rotates and tints it.
       Mostly white so UnitManager's idle/hover tint colours read correctly. Cyan/magenta halo ties it to the Tech Bug`,
  `
    <filter id="glow" x="-40%" y="-40%" width="180%" height="180%"><feGaussianBlur stdDeviation="8"/></filter>`,
  `
  <path filter="url(#glow)" fill="#22e5f0" opacity="0.7" d="M52,44 L196,128 L52,212 L88,128 Z"/>
  <path filter="url(#glow)" fill="#ff3dbe" opacity="0.55" d="M64,44 L208,128 L64,212 L100,128 Z"/>
  <path fill="#ffffff" stroke="#0a0e16" stroke-width="10" stroke-linejoin="miter"
        d="M60,44 L204,128 L60,212 L98,128 Z"/>
  <path fill="#e6fcff" opacity="0.9" d="M86,92 L150,128 L86,164 L104,128 Z"/>
  <rect x="112" y="122" width="40" height="4" fill="#22e5f0" opacity="0.8"/>`);

// ---------- Fabricator border (9-slice: 96 px borders in this 256 viewBox -> 192 px at 512) ----------
sprites.fabricator_frame = svg(
  `Fabricator viewport frame, 9-slice: the outer 96 units on each side are border, the centre is empty.
       Gunmetal panels, rivets, cyan status lights and amber warning chevrons in the corners`,
  `
    <linearGradient id="metal" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#4a525e"/>
      <stop offset="0.5" stop-color="#3a414c"/>
      <stop offset="1" stop-color="#2b3038"/>
    </linearGradient>
    <linearGradient id="lip" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#6b7480"/>
      <stop offset="1" stop-color="#1e2229"/>
    </linearGradient>
    <pattern id="chev" width="16" height="16" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
      <rect width="16" height="16" fill="#2b3038"/><rect width="8" height="16" fill="#f2b84b"/>
    </pattern>
    <filter id="glow" x="-100%" y="-100%" width="300%" height="300%"><feGaussianBlur stdDeviation="2.5"/></filter>`,
  `
  <path fill="url(#metal)" fill-rule="evenodd" d="M0,0 H256 V256 H0 Z M96,96 V160 H160 V96 Z"/>
  <!-- outer bevel -->
  <rect x="3" y="3" width="250" height="250" rx="10" fill="none" stroke="#1a1e24" stroke-width="6"/>
  <rect x="9" y="9" width="238" height="238" rx="7" fill="none" stroke="#6b7480" stroke-width="2" opacity="0.6"/>
  <!-- inner viewport lip (hugs the board edge) -->
  <path fill="url(#lip)" fill-rule="evenodd" d="M82,82 H174 V174 H82 Z M96,96 V160 H160 V96 Z"/>
  <rect x="95" y="95" width="66" height="66" fill="none" stroke="#3fe0e8" stroke-width="2" opacity="0.8"/>
  <rect x="82" y="82" width="92" height="92" fill="none" stroke="#1a1e24" stroke-width="3"/>
  <!-- panel seams (mid-edge pieces are stretched, so only straight seams there) -->
  <g stroke="#1a1e24" stroke-width="3">
    <line x1="16" y1="48" x2="72" y2="48"/><line x1="184" y1="48" x2="240" y2="48"/>
    <line x1="16" y1="208" x2="72" y2="208"/><line x1="184" y1="208" x2="240" y2="208"/>
    <line x1="48" y1="16" x2="48" y2="72"/><line x1="208" y1="16" x2="208" y2="72"/>
    <line x1="48" y1="184" x2="48" y2="240"/><line x1="208" y1="184" x2="208" y2="240"/>
  </g>
  <!-- straight trim lines along every edge -->
  <g stroke="#59616d" stroke-width="2" opacity="0.8">
    <line x1="96" y1="30" x2="160" y2="30"/><line x1="96" y1="226" x2="160" y2="226"/>
    <line x1="30" y1="96" x2="30" y2="160"/><line x1="226" y1="96" x2="226" y2="160"/>
  </g>
  <g stroke="#3fe0e8" stroke-width="2" opacity="0.5">
    <line x1="96" y1="62" x2="160" y2="62"/><line x1="96" y1="194" x2="160" y2="194"/>
    <line x1="62" y1="96" x2="62" y2="160"/><line x1="194" y1="96" x2="194" y2="160"/>
  </g>
  <!-- amber warning chevrons in the corners -->
  <g stroke="#1a1e24" stroke-width="2">
    <rect x="18" y="18" width="24" height="24" fill="url(#chev)"/><rect x="214" y="18" width="24" height="24" fill="url(#chev)"/>
    <rect x="18" y="214" width="24" height="24" fill="url(#chev)"/><rect x="214" y="214" width="24" height="24" fill="url(#chev)"/>
  </g>
  <!-- rivets -->
  <g fill="#7b8490" stroke="#1a1e24" stroke-width="1.5">
    <circle cx="62" cy="24" r="4"/><circle cx="24" cy="62" r="4"/><circle cx="194" cy="24" r="4"/><circle cx="232" cy="62" r="4"/>
    <circle cx="62" cy="232" r="4"/><circle cx="24" cy="194" r="4"/><circle cx="194" cy="232" r="4"/><circle cx="232" cy="194" r="4"/>
  </g>
  <!-- status lights -->
  <g filter="url(#glow)"><circle cx="66" cy="66" r="5" fill="#3fe0e8"/><circle cx="190" cy="66" r="5" fill="#3fe0e8"/>
    <circle cx="66" cy="190" r="5" fill="#ff3dbe"/><circle cx="190" cy="190" r="5" fill="#3fe0e8"/></g>
  <g fill="#ffffff"><circle cx="66" cy="66" r="2"/><circle cx="190" cy="66" r="2"/><circle cx="66" cy="190" r="2"/><circle cx="190" cy="190" r="2"/></g>`);

// ---------- write + render ----------
for (const [name, text] of Object.entries(sprites)) {
  if (only.length && !only.includes(name)) continue;
  fs.writeFileSync(path.join(SRC, name + '.svg'), text);
  const png = new Resvg(text, { fitTo: { mode: 'width', value: 512 } }).render().asPng();
  fs.writeFileSync(path.join(OUT, name + '.png'), png);
  console.log('wrote', name);
}
