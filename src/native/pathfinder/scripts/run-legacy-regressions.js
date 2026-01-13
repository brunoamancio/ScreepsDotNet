#!/usr/bin/env node

const fs = require('fs');
const path = require('path');

const repoRoot = path.resolve(__dirname, '../../../..');
const driverRoot = path.resolve(repoRoot, '..', 'ScreepsNodeJs', 'driver');
const nativeModulePath = path.join(driverRoot, 'native', 'build', 'Release', 'native.node');

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

const plainTerrain = room => ({ room, terrain: '0'.repeat(2500) });

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

for (const test of regressionCases) {
  const { name, result } = runCase(test);
  const expected = test.expected;
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
if (mismatches > 0) {
  console.error('Legacy node driver regressions diverged. See report for details.');
  process.exit(2);
}

console.log('All regression cases match legacy node driver output.');
