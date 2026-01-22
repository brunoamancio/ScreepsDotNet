const path = require('path');
const { loadRoomObjects, loadRoomTerrain } = require('./fixture-loader');

/**
 * Executes Node.js processor with fixture data
 * @param {any} db - MongoDB database
 * @param {any} fixture - Loaded fixture
 * @returns {Promise<{mutations: any, stats: any, eventLog: any}>}
 */
async function executeProcessor(db, fixture) {
    console.log(`Executing processor for room ${fixture.room}...`);

    // Load room data
    const roomObjects = await loadRoomObjects(db, fixture.room);
    const roomTerrain = await loadRoomTerrain(db, fixture.room);

    // Find controller if it exists
    const roomController = Object.values(roomObjects).find(obj => obj.type === 'controller');

    // Mock bulk writer to capture mutations
    const bulkMutations = {
        updates: [],
        inserts: [],
        deletes: []
    };

    const mockBulk = {
        update: function(obj, changes) {
            bulkMutations.updates.push({
                id: obj._id,
                changes: changes
            });
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

    // Build scope object expected by processor
    const scope = {
        roomObjects: roomObjects,
        roomTerrain: roomTerrain,
        roomController: roomController || null,
        gameTime: fixture.gameTime,
        bulk: mockBulk,
        stats: mockStats,
        eventLog: eventLog
    };

    console.log(`  Processing ${Object.keys(fixture.intents || {}).length} users with intents...`);

    // Execute all intents
    let intentCount = 0;
    for (const [userId, userIntents] of Object.entries(fixture.intents || {})) {
        for (const [objectId, intents] of Object.entries(userIntents)) {
            const object = scope.roomObjects[objectId];
            if (!object) {
                console.warn(`    WARNING: Object ${objectId} not found in room objects`);
                continue;
            }

            for (const intent of intents) {
                try {
                    const intentProcessor = requireIntentProcessor(intent.intent);
                    if (intentProcessor) {
                        intentProcessor(object, intent, scope);
                        intentCount++;
                    } else {
                        console.warn(`    WARNING: No processor found for intent: ${intent.intent}`);
                    }
                } catch (error) {
                    console.error(`    ERROR processing intent ${intent.intent} for ${objectId}:`, error.message);
                }
            }
        }
    }

    console.log(`  ✓ Processed ${intentCount} intents`);
    console.log(`  ✓ Captured ${bulkMutations.updates.length} updates, ${bulkMutations.inserts.length} inserts, ${bulkMutations.deletes.length} deletes`);

    return {
        mutations: bulkMutations,
        stats: stats,
        eventLog: eventLog
    };
}

/**
 * Requires the appropriate intent processor from the Screeps engine
 * @param {string} intentName - Intent name (e.g., "harvest", "attack")
 * @returns {Function|null}
 */
function requireIntentProcessor(intentName) {
    const enginePath = path.resolve(__dirname, '../../screeps-modules/engine/src/processor');

    // Intent name mapping (Node.js processor file paths)
    const intentMap = {
        // Creep intents
        'harvest': 'intents/creeps/harvest',
        'attack': 'intents/creeps/attack',
        'attackController': 'intents/creeps/attackController',
        'build': 'intents/creeps/build',
        'claimController': 'intents/creeps/claimController',
        'dismantle': 'intents/creeps/dismantle',
        'drop': 'intents/creeps/drop',
        'generateSafeMode': 'intents/creeps/generateSafeMode',
        'heal': 'intents/creeps/heal',
        'pickup': 'intents/creeps/pickup',
        'pull': 'intents/creeps/pull',
        'rangedAttack': 'intents/creeps/rangedAttack',
        'rangedHeal': 'intents/creeps/rangedHeal',
        'rangedMassAttack': 'intents/creeps/rangedMassAttack',
        'repair': 'intents/creeps/repair',
        'reserveController': 'intents/creeps/reserveController',
        'say': 'intents/creeps/say',
        'suicide': 'intents/creeps/suicide',
        'transfer': 'intents/creeps/transfer',
        'upgradeController': 'intents/creeps/upgradeController',
        'withdraw': 'intents/creeps/withdraw',

        // Movement
        'move': 'intents/movement',
        'moveTo': 'intents/movement',

        // Structure intents
        'destroyStructure': 'intents/destroyStructure',

        // Lab intents
        'runReaction': 'intents/labs/runReaction',
        'boostCreep': 'intents/labs/boostCreep',
        'unboostCreep': 'intents/labs/unboostCreep',

        // Link intents
        'transferEnergy': 'intents/links/transferEnergy',

        // Nuker intents
        'launchNuke': 'intents/nukers/launchNuke',

        // Power spawn intents
        'processPower': 'intents/power-spawns/processPower',

        // Spawn intents
        'spawnCreep': 'intents/spawns/spawnCreep',
        'renewCreep': 'intents/spawns/renewCreep',
        'recycleCreep': 'intents/spawns/recycleCreep',

        // Terminal intents
        'send': 'intents/terminal/send',

        // Tower intents
        'tower-attack': 'intents/towers/attack',
        'tower-heal': 'intents/towers/heal',
        'tower-repair': 'intents/towers/repair',

        // Factory intents
        'produce': 'intents/factories/produce',

        // Observer intents
        'observeRoom': 'intents/observers/observeRoom',

        // Controller intents
        'activateSafeMode': 'intents/room-controller/activateSafeMode',
        'unclaim': 'intents/room-controller/unclaim'
    };

    const processorPath = intentMap[intentName];
    if (!processorPath) {
        return null;
    }

    try {
        const fullPath = path.join(enginePath, processorPath);
        return require(fullPath);
    } catch (error) {
        console.error(`Failed to load processor at ${processorPath}:`, error.message);
        return null;
    }
}

module.exports = {
    executeProcessor
};
