#!/usr/bin/env node

const fs = require('fs');
const path = require('path');

const repoRoot = path.resolve(__dirname, '../../../..');
const driverRoot = path.resolve(repoRoot, '..', 'ScreepsNodeJs', 'driver');
const nativeModulePath = path.join(driverRoot, 'native', 'build', 'Release', 'native.node');
const args = process.argv.slice(2);
let baselinePath;
let expectationPath;

for (let i = 0; i < args.length; i++) {
  if (args[i] === '--baseline') {
    if (i + 1 >= args.length) {
      console.error('--baseline requires a path');
      process.exit(1);
    }
    baselinePath = path.resolve(repoRoot, args[i + 1]);
    i += 1;
  } else if (args[i] === '--expect') {
    if (i + 1 >= args.length) {
      console.error('--expect requires a path');
      process.exit(1);
    }
    expectationPath = path.resolve(repoRoot, args[i + 1]);
    i += 1;
  } else {
    console.warn(`Unknown argument ignored: ${args[i]}`);
  }
}

if (!fs.existsSync(nativeModulePath)) {
  console.error('Native module not built. Run `npx node-gyp rebuild -C native` inside ScreepsNodeJs/driver.');
  process.exit(1);
}

const pathfinderFactory = require(path.join(driverRoot, 'lib', 'path-finder'));
const native = require(nativeModulePath);
const { make, search } = pathfinderFactory.create(native);

class RoomPosition {
  constructor(x, y, roomName) {
    this.x = x | 0;
    this.y = y | 0;
    this.roomName = roomName;
  }
}

make({ RoomPosition });

const WALL_GAP_CASE = 'wall-gap';
const CONTROLLER_CORRIDOR_CASE = 'controller-corridor';
const TOWER_COST_CASE = 'tower-cost';
const FLEE_MULTI_ROOM_CASE = 'flee-multi-room';
const DENSE_CORRIDOR_CASE = 'dense-corridor';
const CONTROLLER_UPGRADE_CREEPS_CASE = 'controller-upgrade-creeps';
const TOWER_POWER_CHOKE_CASE = 'tower-power-choke';

const plainTerrain = room => ({ room, terrain: '0'.repeat(2500) });

function columnWallTerrain(room, columnX, gapStartY, gapLength) {
  const tiles = [];
  for (let y = 0; y < 50; y++) {
    for (let x = 0; x < 50; x++) {
      const blocked = x === columnX && (y < gapStartY || y >= gapStartY + gapLength);
      tiles.push(blocked ? '1' : '0');
    }
  }
  return { room, terrain: tiles.join('') };
}

function controllerCorridorTerrain(room) {
  const tiles = [];
  for (let y = 0; y < 50; y++) {
    for (let x = 0; x < 50; x++) {
      const blocked = x >= 24 && x <= 26 && y >= 19 && y <= 31 && !(x === 25 && y === 20);
      tiles.push(blocked ? '1' : '0');
    }
  }
  return { room, terrain: tiles.join('') };
}

function denseCorridorTerrain(room) {
  const tiles = new Array(2500).fill('1');
  let x = 2;
  let y = 2;
  let direction = 1;

  const carve = (cx, cy) => {
    const idx = cy * 50 + cx;
    tiles[idx] = '0';
  };

  carve(x, y);

  while (true) {
    const targetX = direction > 0 ? 47 : 2;
    while (x !== targetX) {
      x += direction;
      carve(x, y);
    }

    if (y >= 47) {
      break;
    }

    for (let step = 0; step < 3 && y < 47; step++) {
      y += 1;
      carve(x, y);
    }

    if (y >= 47) {
      break;
    }

    direction *= -1;
  }

  return { room, terrain: tiles.join('') };
}

function createControllerUpgradeCostMatrix() {
  const matrix = new Uint8Array(2500);
  const creepPositions = [
    [25, 24],
    [25, 25],
    [25, 26],
    [24, 25],
    [26, 25],
    [24, 27],
    [26, 27],
    [24, 29],
    [25, 29],
    [26, 29],
    [24, 31],
    [25, 31],
    [26, 31]
  ];

  for (const [x, y] of creepPositions) {
    matrix[y * 50 + x] = 255;
  }

  const preferredPath = [];
  for (let x = 5; x <= 45; x++) {
    const offset = x < 25 ? 1 : -1;
    const y = 25 + offset;
    preferredPath.push([x, y]);
  }

  for (const [x, y] of preferredPath) {
    const idx = y * 50 + x;
    if (matrix[idx] !== 255) {
      matrix[idx] = 10;
    }
  }

  return matrix;
}

const createPortalMatrix = edgeY => {
  const matrix = new Uint8Array(2500);
  for (let y = 0; y < 50; y++) {
    for (let x = 0; x < 50; x++) {
      const idx = y * 50 + x;
      matrix[idx] = y === edgeY ? (x === 25 ? 0 : 255) : 0;
    }
  }
  return matrix;
};

const createTowerCostMatrix = () => {
  const matrix = new Uint8Array(2500);
  for (let y = 0; y < 50; y++) {
    for (let x = 0; x < 50; x++) {
      const idx = y * 50 + x;
      matrix[idx] = (x >= 20 && x <= 30 && y >= 20 && y <= 30) ? 255 : 0;
    }
  }
  // carve a diagonal corridor
  for (let i = 0; i < 11; i++) {
    const x = 20 + i;
    const y = 30 - i;
    const idx = y * 50 + x;
    matrix[idx] = 0;
  }
  return matrix;
};

const createTowerPowerCostMatrix = () => {
  const matrix = new Uint8Array(2500);
  const towerZones = [
    { x1: 15, y1: 15, x2: 20, y2: 20 },
    { x1: 30, y1: 30, x2: 35, y2: 35 },
    { x1: 20, y1: 32, x2: 29, y2: 41 }
  ];

  for (const zone of towerZones) {
    for (let y = zone.y1; y <= zone.y2; y++) {
      for (let x = zone.x1; x <= zone.x2; x++) {
        matrix[y * 50 + x] = 200;
      }
    }
  }

  const powerNodes = [
    [18, 25],
    [32, 24],
    [27, 34]
  ];
  for (const [x, y] of powerNodes) {
    matrix[y * 50 + x] = 255;
  }

  for (let i = 0; i < 15; i++) {
    const x = 5 + i;
    const y = 25 + Math.sin(i / 2) * 5 | 0;
    matrix[y * 50 + x] = 1;
  }

  for (let i = 0; i < 15; i++) {
    const x = 20 + i;
    const y = 20 + i;
    matrix[y * 50 + x] = 1;
  }

  for (let i = 0; i < 10; i++) {
    const x = 35 + i;
    const y = 30 - i;
    matrix[y * 50 + x] = 1;
  }

  return matrix;
};

const expectations = loadExpectations(expectationPath);

const regressionCases = [
  {
    name: 'multi-room',
    rooms: [plainTerrain('W0N0'), plainTerrain('W0N1')],
    origin: new RoomPosition(25, 25, 'W0N0'),
    goals: [new RoomPosition(25, 25, 'W0N1')],
    options: { maxRooms: 4, maxOps: 10_000 },
    expected: {
      incomplete: false,
      cost: 50,
      ops: 5,
      path: [
        ['W0N1', 25, 25],
        ['W0N1', 25, 26],
        ['W0N1', 25, 27],
        ['W0N1', 25, 28],
        ['W0N1', 25, 29],
        ['W0N1', 25, 30],
        ['W0N1', 25, 31],
        ['W0N1', 25, 32],
        ['W0N1', 25, 33],
        ['W0N1', 25, 34],
        ['W0N1', 25, 35],
        ['W0N1', 25, 36],
        ['W0N1', 25, 37],
        ['W0N1', 25, 38],
        ['W0N1', 25, 39],
        ['W0N1', 25, 40],
        ['W0N1', 25, 41],
        ['W0N1', 25, 42],
        ['W0N1', 25, 43],
        ['W0N1', 25, 44],
        ['W0N1', 25, 45],
        ['W0N1', 25, 46],
        ['W0N1', 25, 47],
        ['W0N1', 25, 48],
        ['W0N1', 24, 49],
        ['W0N0', 24, 0],
        ['W0N0', 24, 1],
        ['W0N0', 24, 2],
        ['W0N0', 24, 3],
        ['W0N0', 24, 4],
        ['W0N0', 24, 5],
        ['W0N0', 24, 6],
        ['W0N0', 24, 7],
        ['W0N0', 24, 8],
        ['W0N0', 24, 9],
        ['W0N0', 24, 10],
        ['W0N0', 24, 11],
        ['W0N0', 24, 12],
        ['W0N0', 24, 13],
        ['W0N0', 24, 14],
        ['W0N0', 24, 15],
        ['W0N0', 24, 16],
        ['W0N0', 24, 17],
        ['W0N0', 24, 18],
        ['W0N0', 24, 19],
        ['W0N0', 24, 20],
        ['W0N0', 24, 21],
        ['W0N0', 24, 22],
        ['W0N0', 24, 23],
        ['W0N0', 24, 24]
      ]
    }
  },
  {
    name: 'flee-baseline',
    rooms: [plainTerrain('W0N0'), plainTerrain('W0N1')],
    origin: new RoomPosition(25, 25, 'W0N0'),
    goals: [{ pos: new RoomPosition(25, 25, 'W0N0'), range: 3 }],
    options: { flee: true, maxRooms: 2, maxOps: 10_000 },
    expected: {
      incomplete: false,
      cost: 3,
      ops: 2,
      path: [
        ['W0N0', 22, 22],
        ['W0N0', 23, 23],
        ['W0N0', 24, 24]
      ]
    }
  },
  {
    name: FLEE_MULTI_ROOM_CASE,
    rooms: [plainTerrain('W0N0'), plainTerrain('W1N0'), plainTerrain('W2N0')],
    origin: new RoomPosition(10, 25, 'W0N0'),
    goals: [{ pos: new RoomPosition(10, 25, 'W0N0'), range: 5 }],
    options: { flee: true, maxRooms: 3, maxOps: 20_000 },
    expected: {
      incomplete: false,
      cost: 5,
      ops: 4,
      path: [
        ['W0N0', 5, 20],
        ['W0N0', 6, 21],
        ['W0N0', 7, 22],
        ['W0N0', 8, 23],
        ['W0N0', 9, 24]
      ]
    }
  },
  {
    name: 'portal-callback',
    rooms: [plainTerrain('W0N0'), plainTerrain('W0N1')],
    origin: new RoomPosition(10, 10, 'W0N0'),
    goals: [new RoomPosition(40, 40, 'W0N1')],
    options: {
      maxRooms: 4,
      maxOps: 20_000,
      roomCallback: roomName => {
        if (roomName === 'W0N0') return { _bits: createPortalMatrix(0) };
        if (roomName === 'W0N1') return { _bits: createPortalMatrix(49) };
        return null;
      }
    },
    expected: {
      incomplete: false,
      cost: 31,
      ops: 45,
      path: [
        ['W0N1', 40, 40],
        ['W0N1', 39, 40],
        ['W0N1', 38, 41],
        ['W0N1', 37, 42],
        ['W0N1', 36, 43],
        ['W0N1', 35, 44],
        ['W0N1', 34, 45],
        ['W0N1', 33, 46],
        ['W0N1', 32, 47],
        ['W0N1', 31, 48],
        ['W0N1', 30, 49],
        ['W0N0', 30, 0],
        ['W0N0', 29, 1],
        ['W0N0', 28, 1],
        ['W0N0', 27, 1],
        ['W0N0', 26, 1],
        ['W0N0', 25, 1],
        ['W0N0', 24, 1],
        ['W0N0', 23, 1],
        ['W0N0', 22, 1],
        ['W0N0', 21, 1],
        ['W0N0', 20, 1],
        ['W0N0', 19, 1],
        ['W0N0', 18, 2],
        ['W0N0', 17, 3],
        ['W0N0', 16, 4],
        ['W0N0', 15, 5],
        ['W0N0', 14, 6],
        ['W0N0', 13, 7],
        ['W0N0', 12, 8],
        ['W0N0', 11, 9]
      ]
    }
  }
  ,
  {
    name: WALL_GAP_CASE,
    rooms: [columnWallTerrain('W0N0', 25, 20, 10)],
    origin: new RoomPosition(5, 25, 'W0N0'),
    goals: [new RoomPosition(45, 25, 'W0N0')],
    options: { maxRooms: 1, maxOps: 50_000 },
    expected: {
      incomplete: false,
      cost: 40,
      ops: 67,
      path: [
        ['W0N0', 45, 25],
        ['W0N0', 44, 25],
        ['W0N0', 43, 25],
        ['W0N0', 42, 25],
        ['W0N0', 41, 25],
        ['W0N0', 40, 25],
        ['W0N0', 39, 25],
        ['W0N0', 38, 25],
        ['W0N0', 37, 25],
        ['W0N0', 36, 25],
        ['W0N0', 35, 25],
        ['W0N0', 34, 25],
        ['W0N0', 33, 25],
        ['W0N0', 32, 25],
        ['W0N0', 31, 25],
        ['W0N0', 30, 25],
        ['W0N0', 29, 25],
        ['W0N0', 28, 25],
        ['W0N0', 27, 25],
        ['W0N0', 26, 25],
        ['W0N0', 25, 25],
        ['W0N0', 24, 25],
        ['W0N0', 23, 25],
        ['W0N0', 22, 25],
        ['W0N0', 21, 25],
        ['W0N0', 20, 25],
        ['W0N0', 19, 25],
        ['W0N0', 18, 25],
        ['W0N0', 17, 25],
        ['W0N0', 16, 25],
        ['W0N0', 15, 25],
        ['W0N0', 14, 25],
        ['W0N0', 13, 25],
        ['W0N0', 12, 25],
        ['W0N0', 11, 25],
        ['W0N0', 10, 25],
        ['W0N0', 9, 25],
        ['W0N0', 8, 25],
        ['W0N0', 7, 25],
        ['W0N0', 6, 25]
      ]
    }
  },
  {
    name: CONTROLLER_CORRIDOR_CASE,
    rooms: [controllerCorridorTerrain('W0N0')],
    origin: new RoomPosition(5, 25, 'W0N0'),
    goals: [new RoomPosition(45, 25, 'W0N0')],
    options: { maxRooms: 1, maxOps: 50_000 },
    expected: {
      incomplete: false,
      cost: 40,
      ops: 15,
      path: [
        ['W0N0', 45, 25],
        ['W0N0', 44, 25],
        ['W0N0', 43, 25],
        ['W0N0', 42, 25],
        ['W0N0', 41, 25],
        ['W0N0', 40, 25],
        ['W0N0', 39, 25],
        ['W0N0', 38, 25],
        ['W0N0', 37, 25],
        ['W0N0', 36, 25],
        ['W0N0', 35, 25],
        ['W0N0', 34, 25],
        ['W0N0', 33, 25],
        ['W0N0', 32, 26],
        ['W0N0', 31, 27],
        ['W0N0', 30, 28],
        ['W0N0', 29, 29],
        ['W0N0', 28, 30],
        ['W0N0', 27, 31],
        ['W0N0', 26, 32],
        ['W0N0', 25, 32],
        ['W0N0', 24, 32],
        ['W0N0', 23, 32],
        ['W0N0', 22, 32],
        ['W0N0', 21, 32],
        ['W0N0', 20, 32],
        ['W0N0', 19, 32],
        ['W0N0', 18, 32],
        ['W0N0', 17, 32],
        ['W0N0', 16, 32],
        ['W0N0', 15, 32],
        ['W0N0', 14, 32],
        ['W0N0', 13, 32],
        ['W0N0', 12, 32],
        ['W0N0', 11, 31],
        ['W0N0', 10, 30],
        ['W0N0', 9, 29],
        ['W0N0', 8, 28],
        ['W0N0', 7, 27],
        ['W0N0', 6, 26]
      ]
    }
  },
  {
    name: TOWER_COST_CASE,
    rooms: [plainTerrain('W0N0')],
    origin: new RoomPosition(5, 5, 'W0N0'),
    goals: [new RoomPosition(45, 45, 'W0N0')],
    options: {
      maxRooms: 1,
      maxOps: 50_000,
      roomCallback: roomName => roomName === 'W0N0' ? { _bits: createTowerCostMatrix() } : null
    },
    expected: {
      incomplete: false,
      cost: 50,
      ops: 46,
      path: [
        ['W0N0', 45, 45],
        ['W0N0', 44, 45],
        ['W0N0', 43, 45],
        ['W0N0', 42, 45],
        ['W0N0', 41, 45],
        ['W0N0', 40, 45],
        ['W0N0', 39, 45],
        ['W0N0', 38, 45],
        ['W0N0', 37, 45],
        ['W0N0', 36, 45],
        ['W0N0', 35, 45],
        ['W0N0', 34, 44],
        ['W0N0', 33, 43],
        ['W0N0', 32, 42],
        ['W0N0', 31, 41],
        ['W0N0', 30, 40],
        ['W0N0', 29, 39],
        ['W0N0', 28, 38],
        ['W0N0', 27, 37],
        ['W0N0', 26, 36],
        ['W0N0', 25, 35],
        ['W0N0', 24, 34],
        ['W0N0', 23, 33],
        ['W0N0', 22, 32],
        ['W0N0', 21, 31],
        ['W0N0', 20, 30],
        ['W0N0', 19, 29],
        ['W0N0', 19, 28],
        ['W0N0', 19, 27],
        ['W0N0', 19, 26],
        ['W0N0', 19, 25],
        ['W0N0', 19, 24],
        ['W0N0', 19, 23],
        ['W0N0', 19, 22],
        ['W0N0', 19, 21],
        ['W0N0', 19, 20],
        ['W0N0', 19, 19],
        ['W0N0', 18, 18],
        ['W0N0', 17, 17],
        ['W0N0', 16, 16],
        ['W0N0', 15, 15],
        ['W0N0', 14, 14],
        ['W0N0', 13, 13],
        ['W0N0', 12, 12],
        ['W0N0', 11, 11],
        ['W0N0', 10, 10],
        ['W0N0', 9, 9],
        ['W0N0', 8, 8],
        ['W0N0', 7, 7],
        ['W0N0', 6, 6]
      ]
    }
  },
  {
    name: DENSE_CORRIDOR_CASE,
    rooms: [denseCorridorTerrain('W0N0')],
    origin: new RoomPosition(2, 2, 'W0N0'),
    goals: [new RoomPosition(47, 47, 'W0N0')],
    options: { maxRooms: 1, maxOps: 100_000 },
    expected: {
      incomplete: false,
      cost: 691,
      ops: 59,
      path: undefined
    }
  },
  {
    name: CONTROLLER_UPGRADE_CREEPS_CASE,
    rooms: [controllerCorridorTerrain('W0N0')],
    origin: new RoomPosition(5, 25, 'W0N0'),
    goals: [new RoomPosition(45, 25, 'W0N0')],
    options: {
      maxRooms: 1,
      maxOps: 50_000,
      roomCallback: roomName => roomName === 'W0N0' ? { _bits: createControllerUpgradeCostMatrix() } : null
    },
    expected: {
      incomplete: false,
      cost: 42,
      ops: 41,
      path: undefined
    }
  },
  {
    name: TOWER_POWER_CHOKE_CASE,
    rooms: [plainTerrain('W0N0')],
    origin: new RoomPosition(5, 10, 'W0N0'),
    goals: [new RoomPosition(45, 40, 'W0N0')],
    options: {
      maxRooms: 1,
      maxOps: 60_000,
      roomCallback: roomName => roomName === 'W0N0' ? { _bits: createTowerPowerCostMatrix() } : null
    },
    expected: {
      incomplete: false,
      cost: 0,
      ops: 0,
      path: undefined
    }
  }
];

function normalizePath(path) {
  return (path || []).map(pos => [pos.roomName, pos.x, pos.y]);
}

function runCase(caseDef) {
  pathfinderFactory.init(native, caseDef.rooms);
  const goals = caseDef.goals.map(goal => goal instanceof RoomPosition ? goal : { pos: goal.pos, range: goal.range | 0 });
  const opts = { ...caseDef.options };
  const result = search(caseDef.origin, goals.length === 1 ? goals[0] : goals, opts);
  const originFirst = normalizePath(result.path);
  const targetFirst = originFirst.slice().reverse();
  return {
    name: caseDef.name,
    result: {
      incomplete: result.incomplete,
      cost: result.cost,
      ops: result.ops,
      pathOriginFirst: originFirst,
      pathTargetFirst: targetFirst
    }
  };
}

const summary = [];
let mismatches = 0;

function getExpected(caseDef) {
  if (expectations && expectations.has(caseDef.name)) {
    return expectations.get(caseDef.name);
  }
  return caseDef.expected;
}

for (const test of regressionCases) {
  const { name, result } = runCase(test);
  const expected = getExpected(test);
  if (!expected) {
    summary.push({ name, status: 'skipped', diff: [{ field: 'expected', message: 'No expectation provided.' }], result });
    continue;
  }
  const diff = [];
  if (result.incomplete !== expected.incomplete) {
    diff.push({ field: 'incomplete', expected: expected.incomplete, actual: result.incomplete });
  }
  if (result.cost !== expected.cost) {
    diff.push({ field: 'cost', expected: expected.cost, actual: result.cost });
  }
  if (result.ops !== expected.ops) {
    diff.push({ field: 'ops', expected: expected.ops, actual: result.ops });
  }
  const expectedPath = JSON.stringify(expected.path);
  const actualPath = JSON.stringify(result.pathTargetFirst);
  if (expectedPath !== actualPath) {
    diff.push({ field: 'path', expected: expected.path, actual: result.pathTargetFirst });
  }
  const status = diff.length === 0 ? 'match' : 'mismatch';
  if (status === 'mismatch') {
    mismatches += 1;
  }
  summary.push({ name, status, diff, result });
}

const outputPath = path.join(repoRoot, 'src', 'native', 'pathfinder', 'reports');
fs.mkdirSync(outputPath, { recursive: true });
const reportFile = path.join(outputPath, 'legacy-regressions.json');
fs.writeFileSync(reportFile, JSON.stringify({ generatedAt: new Date().toISOString(), summary }, null, 2));

console.log(`Legacy regression report written to ${path.relative(repoRoot, reportFile)}`);
if (baselinePath) {
  const baseline = {
    generatedAt: new Date().toISOString(),
    cases: summary.map(entry => {
      const canonicalPath = entry.result.pathTargetFirst || entry.result.path || [];
      return {
        name: entry.name,
        ops: entry.result.ops,
        cost: entry.result.cost,
        incomplete: entry.result.incomplete,
        path: canonicalPath
      };
    })
  };
  fs.mkdirSync(path.dirname(baselinePath), { recursive: true });
  fs.writeFileSync(baselinePath, JSON.stringify(baseline, null, 2));
  console.log(`Baseline written to ${path.relative(repoRoot, baselinePath)}`);
}
if (mismatches > 0) {
  console.error('Legacy node driver regressions diverged. See report for details.');
  if (!baselinePath) {
    process.exit(2);
  }
  console.warn('--baseline supplied; wrote updated baseline despite differences.');
} else {
  console.log('All regression cases match legacy node driver output.');
}

function loadExpectations(filePath) {
if (!filePath || !fs.existsSync(filePath)) {
  const label = filePath ? path.relative(repoRoot, filePath) : '<unspecified>';
  console.warn(`Expectation file not found at ${label}; using inline fixtures.`);
  return null;
}

  try {
    const payload = JSON.parse(fs.readFileSync(filePath, 'utf8'));
    const map = new Map();
    const cases = Array.isArray(payload.cases) ? payload.cases : [];
    for (const entry of cases) {
      map.set(entry.name, {
        incomplete: entry.incomplete,
        cost: entry.cost,
        ops: entry.ops,
        path: entry.path
      });
    }
    console.log(`Loaded ${map.size} expectations from ${path.relative(repoRoot, filePath)}`);
    return map;
  } catch (err) {
    console.warn(`Failed to load expectations from ${filePath}:`, err);
    return null;
  }
}
