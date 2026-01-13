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
const CONTROLLER_TIGHT_LIMIT_CASE = 'controller-tight-limit';
const TOWER_POWER_CHOKE_CASE = 'tower-power-choke';
const KEEPER_LAIR_CORRIDOR_CASE = 'keeper-lair-corridor';
const PORTAL_CHAIN_CASE = 'portal-chain';
const POWER_CREEP_FLEE_CASE = 'power-creep-flee';

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

const createPowerCreepFleeMatrix = roomName => {
  const matrix = new Uint8Array(2500);
  const hotZones = roomName === 'W0N0'
    ? [
        { x: 20, y: 20, radius: 5 },
        { x: 35, y: 15, radius: 4 }
      ]
    : [
        { x: 10, y: 30, radius: 4 },
        { x: 25, y: 25, radius: 5 }
      ];

  for (const zone of hotZones) {
    for (let dy = -zone.radius; dy <= zone.radius; dy++) {
      for (let dx = -zone.radius; dx <= zone.radius; dx++) {
        const x = zone.x + dx;
        const y = zone.y + dy;
        if (x < 0 || x >= 50 || y < 0 || y >= 50)
          continue;
        const idx = y * 50 + x;
        const dist = Math.max(Math.abs(dx), Math.abs(dy));
        matrix[idx] = dist <= 2 ? 255 : 180;
      }
    }
  }

  const corridor = roomName === 'W0N0'
    ? [
        ...Array.from({ length: 10 }, (_, i) => [5 + i, 40 - i]),
        ...Array.from({ length: 15 }, (_, i) => [15 + i, 30])
      ]
    : [
        ...Array.from({ length: 10 }, (_, i) => [0 + i, 30 + i]),
        ...Array.from({ length: 10 }, (_, i) => [10 + i, 40])
      ];

  for (const [x, y] of corridor) {
    const idx = y * 50 + x;
    matrix[idx] = 1;
  }

  return matrix;
};

const createKeeperLairMatrix = () => {
  const matrix = new Uint8Array(2500);
  const lairs = [
    { x: 15, y: 35 },
    { x: 25, y: 20 },
    { x: 35, y: 30 }
  ];

  for (const { x, y } of lairs) {
    for (let dy = -5; dy <= 5; dy++) {
      for (let dx = -5; dx <= 5; dx++) {
        const lx = x + dx;
        const ly = y + dy;
        if (lx >= 0 && lx < 50 && ly >= 0 && ly < 50) {
          const idx = ly * 50 + lx;
          const dist = Math.max(Math.abs(dx), Math.abs(dy));
          matrix[idx] = Math.max(matrix[idx], dist <= 2 ? 255 : 180);
        }
      }
    }
  }

  const safePath = [
    ...Array.from({ length: 20 }, (_, i) => [5 + i, 40 - i]),
    ...Array.from({ length: 10 }, (_, i) => [25, 20 - i]),
    ...Array.from({ length: 15 }, (_, i) => [25 + i, 10])
  ];

  for (const [x, y] of safePath) {
    const idx = y * 50 + x;
    matrix[idx] = Math.min(matrix[idx], 5);
  }

  return matrix;
};

const portalChainRooms = () => [
  plainTerrain('W0N0'),
  plainTerrain('W0N1'),
  plainTerrain('W0N2'),
  plainTerrain('W0N3'),
  plainTerrain('E0N0'),
  plainTerrain('E0N1'),
  plainTerrain('E0N2'),
  plainTerrain('E0N3'),
  plainTerrain('W1N0'),
  plainTerrain('W1N1'),
  plainTerrain('W1N2'),
  plainTerrain('W1N3'),
  plainTerrain('W0S1'),
  plainTerrain('E0S1'),
  plainTerrain('W1S1')
];

const createPortalChainMatrix = roomName => {
  const matrix = new Uint8Array(2500);
  matrix.fill(200);

  const carve = (x, y) => {
    if (x >= 0 && x < 50 && y >= 0 && y < 50) {
      matrix[y * 50 + x] = 0;
    }
  };

  if (roomName === 'W0N0') {
    let x = 10;
    let y = 10;
    while (y >= 0) {
      carve(x, y);
      x += 1;
      y -= 1;
    }
    while (x <= 30) {
      carve(x, 0);
      x += 1;
    }
    for (let cx = 0; cx < 50; cx++) {
      if (cx !== 30) {
        matrix[cx] = 255;
      }
    }
  } else if (roomName === 'W0N1') {
    for (let cx = 0; cx < 50; cx++) {
      matrix[49 * 50 + cx] = cx === 30 ? 0 : 255;
    }
    for (let cy = 49; cy >= 35; cy--) {
      carve(30, cy);
    }
    for (let cx = 30; cx >= 5; cx--) {
      carve(cx, 35);
    }
    for (let cy = 35; cy >= 0; cy--) {
      carve(5, cy);
    }
    for (let cx = 0; cx < 50; cx++) {
      matrix[cx] = cx === 5 ? 0 : 255;
    }
  } else if (roomName === 'W0N2') {
    for (let cy = 49; cy >= 10; cy--) {
      carve(5, cy);
    }
    for (let step = 0; step <= 40; step++) {
      const px = 5 + step;
      const py = 10 + Math.min(30, Math.round(step * 0.75));
      carve(px, py);
    }
  } else {
    matrix.fill(0);
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
    name: CONTROLLER_TIGHT_LIMIT_CASE,
    rooms: [controllerCorridorTerrain('W0N0'), plainTerrain('W0N1')],
    origin: new RoomPosition(5, 25, 'W0N0'),
    goals: [new RoomPosition(45, 25, 'W0N0')],
    options: {
      maxRooms: 2,
      maxOps: 50_000,
      roomCallback: roomName => roomName === 'W0N0' ? { _bits: createControllerUpgradeCostMatrix() } : null
    },
    expected: null
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
  },
  {
    name: KEEPER_LAIR_CORRIDOR_CASE,
    rooms: [plainTerrain('W0N0')],
    origin: new RoomPosition(5, 40, 'W0N0'),
    goals: [new RoomPosition(45, 10, 'W0N0')],
    options: {
      maxRooms: 1,
      maxOps: 70_000,
      roomCallback: roomName => roomName === 'W0N0' ? { _bits: createKeeperLairMatrix() } : null
    },
    expected: {
      incomplete: false,
      cost: 0,
      ops: 0,
      path: undefined
    }
  },
  {
    name: PORTAL_CHAIN_CASE,
    rooms: portalChainRooms(),
    origin: new RoomPosition(10, 10, 'W0N0'),
    goals: [new RoomPosition(45, 40, 'W0N2')],
    options: {
      maxRooms: 4,
      maxOps: 80_000,
      roomCallback: roomName => {
        if (roomName === 'W0N0' || roomName === 'W0N1' || roomName === 'W0N2') {
          return { _bits: createPortalChainMatrix(roomName) };
        }
        return null;
      }
    },
    expected: {
      incomplete: true,
      cost: 3117,
      ops: 3933,
      path: [
        ['W0N1', 35, 0],
        ['W0N1', 34, 1],
        ['W0N1', 33, 2],
        ['W0N1', 32, 3],
        ['W0N1', 31, 4],
        ['W0N1', 30, 5],
        ['W0N1', 29, 5],
        ['W0N1', 28, 5],
        ['W0N1', 27, 5],
        ['W0N1', 26, 5],
        ['W0N1', 25, 5],
        ['W0N1', 24, 5],
        ['W0N1', 23, 5],
        ['W0N1', 22, 5],
        ['W0N1', 21, 5],
        ['W0N1', 20, 5],
        ['W0N1', 19, 5],
        ['W0N1', 18, 5],
        ['W0N1', 17, 5],
        ['W0N1', 16, 5],
        ['W0N1', 15, 5],
        ['W0N1', 14, 5],
        ['W0N1', 13, 5],
        ['W0N1', 12, 5],
        ['W0N1', 11, 5],
        ['W0N1', 10, 5],
        ['W0N1', 9, 5],
        ['W0N1', 8, 5],
        ['W0N1', 7, 5],
        ['W0N1', 6, 5],
        ['W0N1', 5, 5],
        ['W0N1', 4, 5],
        ['W0N1', 3, 5],
        ['W0N1', 2, 5],
        ['W0N1', 1, 5],
        ['W0N1', 0, 5],
        ['W1N1', 49, 5],
        ['W1N1', 48, 6],
        ['W1N1', 48, 7],
        ['W1N1', 48, 8],
        ['W1N1', 48, 9],
        ['W1N1', 48, 10],
        ['W1N1', 48, 11],
        ['W1N1', 48, 12],
        ['W1N1', 48, 13],
        ['W1N1', 48, 14],
        ['W1N1', 48, 15],
        ['W1N1', 48, 16],
        ['W1N1', 48, 17],
        ['W1N1', 48, 18],
        ['W1N1', 48, 19],
        ['W1N1', 48, 20],
        ['W1N1', 48, 21],
        ['W1N1', 48, 22],
        ['W1N1', 48, 23],
        ['W1N1', 48, 24],
        ['W1N1', 48, 25],
        ['W1N1', 48, 26],
        ['W1N1', 48, 27],
        ['W1N1', 48, 28],
        ['W1N1', 48, 29],
        ['W1N1', 48, 30],
        ['W1N1', 48, 31],
        ['W1N1', 48, 32],
        ['W1N1', 48, 33],
        ['W1N1', 48, 34],
        ['W1N1', 48, 35],
        ['W1N1', 48, 36],
        ['W1N1', 48, 37],
        ['W1N1', 48, 38],
        ['W1N1', 48, 39],
        ['W1N1', 48, 40],
        ['W1N1', 48, 41],
        ['W1N1', 48, 42],
        ['W1N1', 48, 43],
        ['W1N1', 48, 44],
        ['W1N1', 48, 45],
        ['W1N1', 48, 46],
        ['W1N1', 48, 47],
        ['W1N1', 48, 48],
        ['W1N1', 47, 49],
        ['W1N0', 47, 0],
        ['W1N0', 47, 1],
        ['W1N0', 47, 2],
        ['W1N0', 47, 3],
        ['W1N0', 47, 4],
        ['W1N0', 47, 5],
        ['W1N0', 47, 6],
        ['W1N0', 47, 7],
        ['W1N0', 47, 8],
        ['W1N0', 47, 9],
        ['W1N0', 47, 10],
        ['W1N0', 47, 11],
        ['W1N0', 47, 12],
        ['W1N0', 47, 13],
        ['W1N0', 47, 14],
        ['W1N0', 47, 15],
        ['W1N0', 47, 16],
        ['W1N0', 47, 17],
        ['W1N0', 47, 18],
        ['W1N0', 47, 19],
        ['W1N0', 47, 20],
        ['W1N0', 47, 21],
        ['W1N0', 47, 22],
        ['W1N0', 47, 23],
        ['W1N0', 47, 24],
        ['W1N0', 47, 25],
        ['W1N0', 47, 26],
        ['W1N0', 47, 27],
        ['W1N0', 47, 28],
        ['W1N0', 48, 29],
        ['W1N0', 49, 30],
        ['W0N0', 0, 30],
        ['W0N0', 1, 29],
        ['W0N0', 1, 28],
        ['W0N0', 1, 27],
        ['W0N0', 1, 26],
        ['W0N0', 1, 25],
        ['W0N0', 1, 24],
        ['W0N0', 1, 23],
        ['W0N0', 1, 22],
        ['W0N0', 1, 21],
        ['W0N0', 1, 20],
        ['W0N0', 1, 19],
        ['W0N0', 2, 18],
        ['W0N0', 3, 17],
        ['W0N0', 4, 16],
        ['W0N0', 5, 15],
        ['W0N0', 6, 14],
        ['W0N0', 7, 13],
        ['W0N0', 8, 12],
        ['W0N0', 9, 11]
      ]
    }
  }
  ,
  {
    name: POWER_CREEP_FLEE_CASE,
    rooms: [plainTerrain('W0N0'), plainTerrain('W1N0')],
    origin: new RoomPosition(8, 38, 'W0N0'),
    goals: [{ pos: new RoomPosition(8, 38, 'W0N0'), range: 6 }],
    options: {
      flee: true,
      maxRooms: 3,
      maxOps: 50_000,
      roomCallback: roomName => {
        if (roomName === 'W0N0' || roomName === 'W1N0')
          return { _bits: createPowerCreepFleeMatrix(roomName) };
        return null;
      }
    },
    expected: null
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
  const expected = buildExpectedRecord(getExpected(test));
  if (!expected) {
    summary.push({ name, status: 'skipped', diff: [{ field: 'expected', message: 'No expectation provided.' }], result });
    continue;
  }

  const diff = [];
  if (result.incomplete !== expected.incomplete)
    diff.push({ field: 'incomplete', expected: expected.incomplete, actual: result.incomplete });
  if (result.cost !== expected.cost)
    diff.push({ field: 'cost', expected: expected.cost, actual: result.cost });
  if (result.ops !== expected.ops)
    diff.push({ field: 'ops', expected: expected.ops, actual: result.ops });

  if (!pathsEqual(expected.pathOriginFirst, result.pathOriginFirst))
    diff.push({ field: 'path', expected: expected.pathOriginFirst, actual: result.pathOriginFirst });

  const status = diff.length === 0 ? 'match' : 'mismatch';
  if (status === 'mismatch')
    mismatches += 1;

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
    cases: summary.map(entry => ({
      name: entry.name,
      ops: entry.result.ops,
      cost: entry.result.cost,
      incomplete: entry.result.incomplete,
      path: entry.result.pathOriginFirst || []
    }))
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
      const originFirst = clonePath(entry.path || []);
      const targetFirst = originFirst.slice().reverse();
      map.set(entry.name, {
        incomplete: entry.incomplete,
        cost: entry.cost,
        ops: entry.ops,
        pathOriginFirst: originFirst,
        pathTargetFirst: targetFirst
      });
    }
    console.log(`Loaded ${map.size} expectations from ${path.relative(repoRoot, filePath)}`);
    return map;
  } catch (err) {
    console.warn(`Failed to load expectations from ${filePath}:`, err);
    return null;
  }
}

function buildExpectedRecord(raw) {
  if (!raw) return null;

  const expected = {
    incomplete: raw.incomplete,
    cost: raw.cost,
    ops: raw.ops
  };

  const pathOriginFirst = raw.pathOriginFirst ? clonePath(raw.pathOriginFirst) : null;
  const pathTargetFirst = raw.pathTargetFirst ? clonePath(raw.pathTargetFirst) : null;
  const fallbackPath = Array.isArray(raw.path) ? clonePath(raw.path) : null;

  if (!pathOriginFirst && !pathTargetFirst && fallbackPath)
    expected.pathTargetFirst = fallbackPath;
  else {
    expected.pathOriginFirst = pathOriginFirst || null;
    expected.pathTargetFirst = pathTargetFirst || null;
  }

  if (!expected.pathOriginFirst && expected.pathTargetFirst)
    expected.pathOriginFirst = expected.pathTargetFirst.slice().reverse();
  if (!expected.pathTargetFirst && expected.pathOriginFirst)
    expected.pathTargetFirst = expected.pathOriginFirst.slice().reverse();

  if (!expected.pathOriginFirst)
    expected.pathOriginFirst = [];
  if (!expected.pathTargetFirst)
    expected.pathTargetFirst = [];
  return expected;
}

function clonePath(path) {
  return path.map(step => step.slice());
}

function pathsEqual(left, right) {
  if (left.length !== right.length) return false;
  for (let i = 0; i < left.length; i++) {
    const a = left[i];
    const b = right[i];
    if (a[0] !== b[0] || a[1] !== b[1] || a[2] !== b[2])
      return false;
  }
  return true;
}
