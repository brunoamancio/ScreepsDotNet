const path = require('path');

/**
 * Executes Screeps engine processors on loaded fixtures and captures mutations.
 */

// Intent name to processor file path mapping
const INTENT_PROCESSOR_MAP = {
  'harvest': 'creeps/harvest',
  'attack': 'creeps/attack',
  'rangedAttack': 'creeps/ranged-attack',
  'heal': 'creeps/heal',
  'move': 'movement',
  'transfer': 'creeps/transfer',
  'withdraw': 'creeps/withdraw',
  'pickup': 'creeps/pickup',
  'drop': 'creeps/drop',
  'build': 'creeps/build',
  'repair': 'creeps/repair',
  'upgradeController': 'creeps/upgrade-controller',
  'attackController': 'creeps/attack-controller',
  'reserveController': 'creeps/reserve-controller',
  'runReaction': 'labs/run-reaction',
  'boostCreep': 'labs/boost-creep',
  'unboostCreep': 'labs/unboost-creep',
  'renewCreep': 'spawns/renew',
  'recycleCreep': 'spawns/recycle',
  'launchNuke': 'nuker/launch',
  'processPower': 'power-spawn/process',
  'produce': 'factory/produce'
};

async function executeProcessor(db, fixture) {
  console.log('Executing processor...');

  // Mock bulk writer to capture mutations
  const bulkMutations = {
    updates: [],
    inserts: [],
    deletes: []
  };

  const mockBulk = {
    update: function(obj, changes) {
      bulkMutations.updates.push({ id: obj._id, changes });
    },
    insert: function(obj) {
      bulkMutations.inserts.push(obj);
    },
    remove: function(id) {
      bulkMutations.deletes.push(id);
    }
  };

  // Mock stats sink
  const stats = {};
  const mockStats = {
    inc: function(key, userId, amount) {
      const statKey = `${userId}.${key}`;
      stats[statKey] = (stats[statKey] || 0) + amount;
    }
  };

  // Mock event log
  const eventLog = [];

  // Load room objects from database
  const roomObjects = {};
  const objects = await db.collection('rooms.objects').find({ room: fixture.room }).toArray();
  objects.forEach(obj => {
    roomObjects[obj._id] = obj;
  });

  // Build scope object expected by processor
  const scope = {
    roomObjects,
    roomTerrain: null,
    roomController: null,
    gameTime: fixture.gameTime,
    bulk: mockBulk,
    stats: mockStats,
    eventLog
  };

  console.log(`  Processing ${Object.keys(fixture.intents || {}).length} users' intents...`);

  // Execute all intents
  try {
    const modulesPath = path.join(__dirname, '../../screeps-modules/engine/src/processor');

    for (const userId in (fixture.intents || {})) {
      for (const objectId in fixture.intents[userId]) {
        const object = roomObjects[objectId];
        if (!object) {
          console.warn(`  Warning: Object ${objectId} not found in room`);
          continue;
        }

        const intents = fixture.intents[userId][objectId];
        for (const intent of intents) {
          const intentName = intent.intent || intent.type || 'unknown';
          const processorPath = INTENT_PROCESSOR_MAP[intentName];

          if (!processorPath) {
            console.warn(`  Warning: Unknown intent type: ${intentName}`);
            continue;
          }

          try {
            const processorFile = path.join(modulesPath, `${processorPath}.js`);
            const intentProcessor = require(processorFile);

            console.log(`  Executing ${intentName} for ${objectId}`);
            intentProcessor(object, intent, scope);
          } catch (error) {
            console.error(`  Error executing ${intentName}:`, error.message);
          }
        }
      }
    }
  } catch (error) {
    console.error('Error executing intents:', error);
    throw error;
  }

  console.log(`  Captured ${bulkMutations.updates.length} updates, ${bulkMutations.inserts.length} inserts, ${bulkMutations.deletes.length} deletes`);
  console.log(`  Captured ${Object.keys(stats).length} stat changes`);

  return {
    mutations: bulkMutations,
    stats,
    eventLog
  };
}

module.exports = { executeProcessor };
