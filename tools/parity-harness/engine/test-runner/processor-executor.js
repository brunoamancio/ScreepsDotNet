const path = require('path');
const { loadRoomObjects, loadRoomTerrain } = require('./fixture-loader');

// Set driver module path before loading any Screeps modules
// This tells the engine to use our driver-shim instead of @screeps/core
process.env.DRIVER_MODULE = path.join(__dirname, '../../screeps-modules/driver-shim.js');

/**
 * Executes Node.js processor with fixture data
 * @param {any} db - MongoDB database
 * @param {any} fixture - Loaded fixture
 * @returns {Promise<{mutations: any, stats: any, eventLog: any}>}
 */
async function executeProcessor(db, fixture) {
    const isMultiRoom = fixture.rooms !== undefined;

    if (isMultiRoom) {
        console.log(`Executing processor for ${Object.keys(fixture.rooms).length} rooms...`);
    } else {
        console.log(`Executing processor for room ${fixture.room}...`);
    }

    // Load room data (support both single-room and multi-room fixtures)
    let roomObjects, roomTerrain, roomController;

    if (isMultiRoom) {
        // Multi-room: load all rooms into single flat object map
        roomObjects = {};
        const allRoomNames = Object.keys(fixture.rooms);

        for (const roomName of allRoomNames) {
            const roomObjs = await loadRoomObjects(db, roomName);
            Object.assign(roomObjects, roomObjs);
        }

        // Use terrain from first room for controller lookup (if needed)
        const firstRoom = allRoomNames[0];
        roomTerrain = await loadRoomTerrain(db, firstRoom);
        roomController = Object.values(roomObjects).find(obj => obj.type === 'controller' && obj.room === firstRoom) || null;
    } else {
        // Single-room: original logic
        roomObjects = await loadRoomObjects(db, fixture.room);
        roomTerrain = await loadRoomTerrain(db, fixture.room);
        roomController = Object.values(roomObjects).find(obj => obj.type === 'controller') || null;
    }

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
            // IMPORTANT: Update the object in-place so subsequent reads see the changes
            // This matches how the real Screeps bulk writer works
            Object.assign(obj, changes);
        },
        insert: function(obj) {
            bulkMutations.inserts.push(obj);
        },
        remove: function(id) {
            bulkMutations.deletes.push(id);
        },
        inc: function(obj, field, amount) {
            // Find existing update for this object, or create new one
            let existingUpdate = bulkMutations.updates.find(u => u.id === obj._id);
            if (!existingUpdate) {
                existingUpdate = {
                    id: obj._id,
                    changes: {}
                };
                bulkMutations.updates.push(existingUpdate);
            }

            // Increment the field
            existingUpdate.changes[field] = (existingUpdate.changes[field] || obj[field] || 0) + amount;
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

    // Initialize actionLog on all room objects (required by official Screeps processors)
    for (const obj of Object.values(roomObjects)) {
        if (!obj.actionLog) {
            obj.actionLog = {};
        }
    }

    // Mock bulk users (for GCL updates, etc.)
    const mockBulkUsers = {
        inc: function(userId, field, amount) {
            // For parity testing, we don't need to track user updates
            // Just silently accept the call
        }
    };

    // Build scope object expected by processor
    const scope = {
        roomObjects: roomObjects,
        roomTerrain: roomTerrain,
        roomController: roomController || null,
        gameTime: fixture.gameTime,
        bulk: mockBulk,
        bulkUsers: mockBulkUsers,
        stats: mockStats,
        eventLog: eventLog
    };

    // Initialize movement module (required for tick processor)
    const movement = requireIntentProcessor('move'); // Load movement module
    if (movement && movement.init) {
        movement.init(scope);
        console.log(`  ✓ Initialized movement module`);
    }

    // Execute pretick processors (NPC AI, etc.) BEFORE intents
    console.log(`  Running pretick processors (NPC AI)...`);
    let pretickCount = 0;
    const pretickIntents = []; // Collect intents from pretick processors

    for (const object of Object.values(roomObjects)) {
        if (object.type === 'creep' && object.user === '3') {
            // Source keeper AI
            try {
                const keeperPretick = requirePretickProcessor('keepers');
                if (keeperPretick) {
                    const result = keeperPretick(object, scope);
                    pretickIntents.push({ objectId: object._id, intents: result });
                    pretickCount++;
                }
            } catch (error) {
                console.error(`    ERROR in keeper pretick for ${object._id}:`, error.message);
                console.error(`    Stack:`, error.stack);
            }
        } else if (object.type === 'creep' && object.user === '2') {
            // Invader AI
            try {
                const invaderPretick = requirePretickProcessor('invaders');
                if (invaderPretick) {
                    const result = invaderPretick(object, scope);
                    pretickIntents.push({ objectId: object._id, intents: result });
                    pretickCount++;
                }
            } catch (error) {
                console.error(`    ERROR in invader pretick for ${object._id}:`, error.message);
                console.error(`    Stack:`, error.stack);
            }
        }
    }
    console.log(`  ✓ Executed ${pretickCount} pretick processors`);

    // Process pretick intents (NPC AI intents)
    console.log(`  Processing ${pretickIntents.length} pretick intent sets...`);
    let pretickIntentCount = 0;
    for (const { objectId, intents } of pretickIntents) {
        const object = scope.roomObjects[objectId];
        if (!object) {
            console.warn(`    WARNING: Pretick object ${objectId} not found in room objects`);
            continue;
        }

        // Process each intent from the pretick processor
        for (const [intentObjectId, objectIntents] of Object.entries(intents)) {
            const intentObject = scope.roomObjects[intentObjectId];
            if (!intentObject) {
                console.warn(`    WARNING: Intent target object ${intentObjectId} not found`);
                continue;
            }

            for (const [intentName, intentData] of Object.entries(objectIntents)) {
                try {
                    const intent = { intent: intentName, ...intentData };
                    const intentProcessor = requireIntentProcessor(intentName);
                    if (intentProcessor) {
                        // Handle both function exports and object exports
                        if (typeof intentProcessor === 'function') {
                            intentProcessor(intentObject, intent, scope);
                            pretickIntentCount++;
                        } else if (typeof intentProcessor === 'object') {
                            // For object exports, try to find the handler method (e.g., movement module)
                            if (intentProcessor[intentName] && typeof intentProcessor[intentName] === 'function') {
                                intentProcessor[intentName](intentObject, intent, scope);
                                pretickIntentCount++;
                            } else {
                                // Skip movement intent - it will be handled by tick processor via movement.execute()
                                if (intentName !== 'move' && intentName !== 'moveTo') {
                                    console.warn(`    WARNING: Intent processor for '${intentName}' is object without '${intentName}' method`);
                                }
                            }
                        } else {
                            console.warn(`    WARNING: Intent processor for '${intentName}' has unexpected type: ${typeof intentProcessor}`);
                        }
                    } else {
                        console.warn(`    WARNING: No processor found for pretick intent: ${intentName}`);
                    }
                } catch (error) {
                    console.error(`    ERROR processing pretick intent ${intentName} for ${intentObjectId}:`, error.message);
                    console.error(`    Stack:`, error.stack);
                }
            }
        }
    }
    console.log(`  ✓ Processed ${pretickIntentCount} pretick intents`);

    // Process intents (handle both single-room and multi-room formats)
    let intentCount = 0;

    if (isMultiRoom) {
        // Multi-room format: fixture.intents[roomName][userId][objectId]
        console.log(`  Processing intents from ${Object.keys(fixture.intents || {}).length} rooms...`);

        for (const [roomName, roomIntents] of Object.entries(fixture.intents || {})) {
            for (const [userId, userIntents] of Object.entries(roomIntents)) {
                for (const [objectId, intents] of Object.entries(userIntents)) {
                    const object = scope.roomObjects[objectId];
                    if (!object) {
                        console.warn(`    WARNING: Object ${objectId} not found in room objects (room ${roomName})`);
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
                            console.error(`    Stack:`, error.stack);
                        }
                    }
                }
            }
        }
    } else {
        // Single-room format: fixture.intents[userId][objectId]
        console.log(`  Processing ${Object.keys(fixture.intents || {}).length} users with intents...`);

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
                        console.error(`    Stack:`, error.stack);
                    }
                }
            }
        }
    }

    console.log(`  ✓ Processed ${intentCount} intents`);

    // Run tick processor for all creeps (applies accumulated damage/healing)
    console.log(`  Running tick processors for creeps...`);
    let tickCount = 0;
    const tickProcessor = requireTickProcessor('creeps');
    if (tickProcessor) {
        for (const object of Object.values(roomObjects)) {
            if (object.type === 'creep') {
                try {
                    tickProcessor(object, scope);
                    tickCount++;
                } catch (error) {
                    console.error(`    ERROR in creep tick for ${object._id}:`, error.message);
                    console.error(`    Stack:`, error.stack);
                }
            }
        }
    }
    console.log(`  ✓ Processed ${tickCount} creep ticks`);

    // Run tick processors for structures (decay, regeneration, etc.)
    console.log(`  Running tick processors for structures...`);
    let structureTickCount = 0;

    // Map structure types to their tick processor paths
    const structureTickProcessors = {
        'rampart': 'intents/ramparts/tick',
        'road': 'intents/roads/tick',
        'constructedWall': 'intents/constructedWalls/tick',
        'container': 'intents/containers/tick',
        'link': 'intents/links/tick',
        'tower': 'intents/towers/tick',
        'extension': 'intents/extensions/tick',
        'storage': 'intents/storages/tick',
        'energy': 'intents/energy/tick',
        'nuke': 'intents/nukes/tick'
    };

    const enginePath = path.resolve(__dirname, '../../screeps-modules/engine/src/processor');

    for (const object of Object.values(roomObjects)) {
        const processorPath = structureTickProcessors[object.type];
        if (processorPath) {
            try {
                const fullPath = path.join(enginePath, processorPath);
                const tickProcessor = require(fullPath);
                if (tickProcessor) {
                    tickProcessor(object, scope);
                    structureTickCount++;
                }
            } catch (error) {
                console.error(`    ERROR in ${object.type} tick for ${object._id}:`, error.message);
                console.error(`    Stack:`, error.stack);
            }
        }
    }

    console.log(`  ✓ Processed ${structureTickCount} structure ticks`);

    // Run global processors (market, power, etc.) for multi-room operations like Terminal.send
    console.log(`  Running global processors...`);
    runGlobalProcessors(fixture, roomObjects, bulkMutations, stats);

    console.log(`  ✓ Captured ${bulkMutations.updates.length} updates, ${bulkMutations.inserts.length} inserts, ${bulkMutations.deletes.length} deletes`);

    return {
        mutations: bulkMutations,
        stats: stats,
        eventLog: eventLog
    };
}

/**
 * Runs global processors (market, power) for cross-room operations
 * @param {any} fixture - Test fixture
 * @param {any} roomObjects - All room objects (flat map)
 * @param {any} bulkMutations - Bulk mutations to update
 * @param {any} stats - Stats object
 */
function runGlobalProcessors(fixture, roomObjects, bulkMutations, stats) {
    const path = require('path');
    const enginePath = path.resolve(__dirname, '../../screeps-modules/engine/src/processor');

    // Load lodash from engine's node_modules
    const engineNodeModulesPath = path.resolve(__dirname, '../../screeps-modules/engine/node_modules');
    const _ = require(path.join(engineNodeModulesPath, 'lodash'));

    // Load market processor
    const marketProcessor = require(path.join(enginePath, 'global-intents/market'));

    // Build parameters for market processor
    const roomObjectsByType = _.groupBy(Object.values(roomObjects), 'type');
    const gameTime = fixture.gameTime;

    // Mock bulk writers for global processor
    const bulkObjects = {
        update: function(obj, changes) {
            let existingUpdate = bulkMutations.updates.find(u => u.id === obj._id);
            if (!existingUpdate) {
                existingUpdate = { id: obj._id, changes: {} };
                bulkMutations.updates.push(existingUpdate);
            }

            // Track the room for multi-room grouping
            // The room isn't part of changes, but we need it to group mutations correctly
            if (obj.room && !existingUpdate.changes.room) {
                existingUpdate.changes.room = obj.room;
            }

            // Handle store updates specially - merge instead of replace
            if (changes.store) {
                if (!existingUpdate.changes.store) {
                    existingUpdate.changes.store = {};
                }
                Object.assign(existingUpdate.changes.store, changes.store);

                // Update object in-place (merge store)
                if (!obj.store) {
                    obj.store = {};
                }
                Object.assign(obj.store, changes.store);

                // Remove store from changes for Object.assign below
                const { store, ...otherChanges } = changes;
                Object.assign(existingUpdate.changes, otherChanges);
                Object.assign(obj, otherChanges);
            } else {
                Object.assign(existingUpdate.changes, changes);
                Object.assign(obj, changes);
            }
        }
    };

    const bulkTransactions = {
        insert: function(transaction) {
            // Store transactions for parity comparison
            if (!bulkMutations.transactions) {
                bulkMutations.transactions = [];
            }
            bulkMutations.transactions.push(transaction);
        }
    };

    const bulkUsers = {
        update: function(user, changes) {
            // No-op for parity testing
        }
    };

    const bulkUsersMoney = {
        inc: function(userId, amount) {
            // Track user money changes
            if (!bulkMutations.userMoney) {
                bulkMutations.userMoney = {};
            }
            bulkMutations.userMoney[userId] = (bulkMutations.userMoney[userId] || 0) + amount;
        }
    };

    const bulkUsersResources = {
        update: function(userId, resourceType, amount) {
            // No-op for parity testing
        }
    };

    const bulkMarketOrders = {
        update: function(orderId, changes) {
            // No-op for parity testing (no market orders in terminal send fixture)
        },
        remove: function(orderId) {
            // No-op for parity testing
        }
    };

    const bulkMarketIntershardOrders = {
        update: function(orderId, changes) {
            // No-op for parity testing
        }
    };

    // Call market processor
    marketProcessor({
        orders: [],  // No market orders in terminal send fixtures
        userIntents: [],  // User intents are processed via room objects (terminal.send property)
        usersById: fixture.users || {},
        gameTime: gameTime,
        roomObjectsByType: roomObjectsByType,
        bulkObjects: bulkObjects,
        bulkUsers: bulkUsers,
        bulkTransactions: bulkTransactions,
        bulkUsersMoney: bulkUsersMoney,
        bulkUsersResources: bulkUsersResources,
        bulkMarketOrders: bulkMarketOrders,
        bulkMarketIntershardOrders: bulkMarketIntershardOrders
    });

    console.log(`    ✓ Market processor complete - captured ${bulkMutations.updates.length} updates, ${(bulkMutations.transactions || []).length} transactions`);

    // Run PowerCreep global processor if global intents are present
    if (fixture.globalIntents || fixture.powerCreeps) {
        runPowerCreepGlobalProcessor(fixture, roomObjects, bulkMutations, stats);
    }
}

/**
 * Runs PowerCreep global processor for power creep management intents
 * @param {any} fixture - Test fixture
 * @param {any} roomObjects - All room objects (flat map)
 * @param {any} bulkMutations - Bulk mutations to update
 * @param {any} stats - Stats object
 */
function runPowerCreepGlobalProcessor(fixture, roomObjects, bulkMutations, stats) {
    const path = require('path');
    const enginePath = path.resolve(__dirname, '../../screeps-modules/engine/src/processor');

    try {
        // Load PowerCreep global processor (located at global-intents/power)
        const powerCreepProcessor = require(path.join(enginePath, 'global-intents/power'));

        // Build power creeps map
        const powerCreepsById = {};
        if (fixture.powerCreeps) {
            for (const [creepId, creepData] of Object.entries(fixture.powerCreeps)) {
                powerCreepsById[creepId] = { _id: creepId, ...creepData };
            }
        }

        // Build user intents array in the format expected by official Screeps engine
        // Official format: { user, intents: { createPowerCreep: [...], renamePowerCreep: [...], ... } }
        const userIntents = [];
        if (fixture.globalIntents) {
            for (const [userId, intentData] of Object.entries(fixture.globalIntents)) {
                // Transform intent records to official format
                const transformedIntents = {};

                for (const intent of (intentData.intents || [])) {
                    const intentType = intent.name; // e.g., "createPowerCreep"

                    if (!transformedIntents[intentType]) {
                        transformedIntents[intentType] = [];
                    }

                    // Transform args array to single intent object
                    // Fixture format: { name: "createPowerCreep", args: [{ name: "MyOp", className: "operator" }] }
                    // Official format: { name: "MyOp", className: "operator" }
                    for (const arg of (intent.args || [])) {
                        transformedIntents[intentType].push(arg);
                    }
                }

                userIntents.push({
                    _id: intentData._id || intentData.id,
                    user: userId,
                    intents: transformedIntents
                });
            }
        }

        // Mock bulk writers for PowerCreep mutations
        const bulkPowerCreeps = {
            update: function(creep, changes) {
                // Track PowerCreep patches
                if (!bulkMutations.powerCreepPatches) {
                    bulkMutations.powerCreepPatches = [];
                }
                bulkMutations.powerCreepPatches.push({
                    id: creep._id,
                    changes: changes
                });

                // Update in-place
                Object.assign(creep, changes);
            },
            insert: function(creep) {
                // Track PowerCreep upserts
                if (!bulkMutations.powerCreepUpserts) {
                    bulkMutations.powerCreepUpserts = [];
                }
                bulkMutations.powerCreepUpserts.push(creep);

                // Add to powerCreepsById so it's available for subsequent operations
                powerCreepsById[creep._id] = creep;
            },
            remove: function(creepId) {
                // Track PowerCreep deletions
                if (!bulkMutations.powerCreepRemovals) {
                    bulkMutations.powerCreepRemovals = [];
                }
                bulkMutations.powerCreepRemovals.push(creepId);

                // Remove from map
                delete powerCreepsById[creepId];
            }
        };

        const bulkObjects = {
            insert: function(obj) {
                // Track room object inserts (for spawning power creeps)
                if (!bulkMutations.inserts) {
                    bulkMutations.inserts = [];
                }
                bulkMutations.inserts.push(obj);

                // Add to roomObjects so it's available
                roomObjects[obj._id] = obj;
            },
            remove: function(objId) {
                // Track room object removes (for suicide)
                if (!bulkMutations.deletes) {
                    bulkMutations.deletes = [];
                }
                bulkMutations.deletes.push(objId);

                // Remove from map
                delete roomObjects[objId];
            }
        };

        const bulkUsers = {
            update: function(user, changes) {
                // No-op for parity testing
            }
        };

        // Call PowerCreep processor (uses official Screeps engine parameter names)
        powerCreepProcessor({
            userIntents: userIntents,
            userPowerCreeps: Object.values(powerCreepsById), // Convert to array
            usersById: fixture.users || {},
            roomObjectsByType: { powerCreep: Object.values(roomObjects).filter(obj => obj.type === 'powerCreep') },
            gameTime: fixture.gameTime,
            bulkUsersPowerCreeps: bulkPowerCreeps, // Renamed for official engine
            bulkObjects: bulkObjects,
            bulkUsers: bulkUsers
        });

        const patchCount = (bulkMutations.powerCreepPatches || []).length;
        const upsertCount = (bulkMutations.powerCreepUpserts || []).length;
        const removeCount = (bulkMutations.powerCreepRemovals || []).length;
        console.log(`    ✓ PowerCreep global processor complete - ${patchCount} patches, ${upsertCount} upserts, ${removeCount} removals`);
    } catch (error) {
        console.warn(`    WARNING: PowerCreep global processor not found or failed: ${error.message}`);
        console.warn(`    This is expected if testing against older Screeps engine versions`);
    }
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
        'transferEnergy': 'intents/links/transfer',

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

/**
 * Requires a pretick processor from the Screeps engine
 * @param {string} npcType - NPC type ("keepers" or "invaders")
 * @returns {Function|null}
 */
function requirePretickProcessor(npcType) {
    const enginePath = path.resolve(__dirname, '../../screeps-modules/engine/src/processor');

    const pretickMap = {
        'keepers': 'intents/creeps/keepers/pretick',
        'invaders': 'intents/creeps/invaders/pretick'
    };

    const processorPath = pretickMap[npcType];
    if (!processorPath) {
        return null;
    }

    try {
        const fullPath = path.join(enginePath, processorPath);
        return require(fullPath);
    } catch (error) {
        console.error(`Failed to load pretick processor at ${processorPath}:`, error.message);
        return null;
    }
}

/**
 * Requires a tick processor from the Screeps engine
 * @param {string} tickType - Tick type ("creeps" or "power-creeps")
 * @returns {Function|null}
 */
function requireTickProcessor(tickType) {
    const enginePath = path.resolve(__dirname, '../../screeps-modules/engine/src/processor');

    const tickMap = {
        'creeps': 'intents/creeps/tick',
        'power-creeps': 'intents/power-creeps/tick'
    };

    const processorPath = tickMap[tickType];
    if (!processorPath) {
        return null;
    }

    try {
        const fullPath = path.join(enginePath, processorPath);
        return require(fullPath);
    } catch (error) {
        console.error(`Failed to load tick processor at ${processorPath}:`, error.message);
        return null;
    }
}

module.exports = {
    executeProcessor
};
