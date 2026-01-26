const { loadRoomObjects } = require('./fixture-loader');

/**
 * Serializes processor output to JSON format matching .NET output
 * @param {any} db - MongoDB database
 * @param {any} fixture - Original fixture
 * @param {any} processorOutput - Output from executeProcessor
 * @returns {Promise<any>}
 */
async function serializeOutput(db, fixture, processorOutput) {
    console.log('Serializing output...');

    const isMultiRoom = fixture.rooms !== undefined;

    if (isMultiRoom) {
        // Multi-room output format
        const output = await serializeMultiRoomOutput(db, fixture, processorOutput);
        return output;
    } else {
        // Single-room output format (original logic)
        const finalState = await computeFinalState(db, fixture, processorOutput.mutations);
        const actionLogs = extractActionLogs(processorOutput.mutations);

        const output = {
            mutations: {
                patches: convertUpdatesToPatchFormat(processorOutput.mutations.updates),
                upserts: processorOutput.mutations.inserts,
                removals: processorOutput.mutations.deletes
            },
            stats: processorOutput.stats,
            actionLogs: actionLogs,
            finalState: finalState,
            transactions: processorOutput.mutations.transactions || [],
            userMoney: processorOutput.mutations.userMoney || {},
            metadata: {
                room: fixture.room,
                gameTime: fixture.gameTime,
                timestamp: new Date().toISOString()
            }
        };

        console.log(`  ✓ Serialized ${Object.keys(finalState).length} objects in final state`);

        return output;
    }
}

/**
 * Serializes multi-room output with mutations grouped by room
 * @param {any} db - MongoDB database
 * @param {any} fixture - Multi-room fixture
 * @param {any} processorOutput - Output from executeProcessor
 * @returns {Promise<any>}
 */
async function serializeMultiRoomOutput(db, fixture, processorOutput) {
    const roomNames = Object.keys(fixture.rooms);

    // Group mutations by room
    const mutationsByRoom = groupMutationsByRoom(processorOutput.mutations, roomNames);

    // Compute final state per room
    const finalStateByRoom = {};
    for (const roomName of roomNames) {
        finalStateByRoom[roomName] = await computeFinalStateForRoom(db, roomName, mutationsByRoom[roomName] || { updates: [], inserts: [], deletes: [] });
    }

    // Build multi-room output structure
    const mutations = {};
    for (const roomName of roomNames) {
        const roomMutations = mutationsByRoom[roomName] || { updates: [], inserts: [], deletes: [] };
        mutations[roomName] = {
            patches: convertUpdatesToPatchFormat(roomMutations.updates),
            upserts: roomMutations.inserts,
            removals: roomMutations.deletes
        };
    }

    const output = {
        mutations: mutations,
        stats: processorOutput.stats,
        actionLogs: extractActionLogs(processorOutput.mutations),
        finalState: finalStateByRoom,
        transactions: processorOutput.mutations.transactions || [],
        userMoney: processorOutput.mutations.userMoney || {},
        powerCreepPatches: processorOutput.mutations.powerCreepPatches || [],
        powerCreepUpserts: processorOutput.mutations.powerCreepUpserts || [],
        powerCreepRemovals: processorOutput.mutations.powerCreepRemovals || [],
        metadata: {
            rooms: roomNames,
            gameTime: fixture.gameTime,
            timestamp: new Date().toISOString()
        }
    };

    const totalObjects = Object.values(finalStateByRoom).reduce((sum, roomState) => sum + Object.keys(roomState).length, 0);
    const powerCreepCount = (processorOutput.mutations.powerCreepPatches || []).length +
                            (processorOutput.mutations.powerCreepUpserts || []).length +
                            (processorOutput.mutations.powerCreepRemovals || []).length;
    console.log(`  ✓ Serialized ${totalObjects} room objects across ${roomNames.length} rooms, ${powerCreepCount} power creep mutations`);

    return output;
}

/**
 * Groups mutations by room based on object's room property
 * @param {any} mutations - All mutations
 * @param {string[]} roomNames - List of room names
 * @returns {any}
 */
function groupMutationsByRoom(mutations, roomNames) {
    const byRoom = {};

    // Initialize empty mutation sets for each room
    for (const roomName of roomNames) {
        byRoom[roomName] = {
            updates: [],
            inserts: [],
            deletes: []
        };
    }

    // Group updates by room
    for (const update of mutations.updates) {
        // Find the room this object belongs to by checking changes.room or looking up in inserts
        const room = update.changes.room || findObjectRoom(update.id, mutations);
        if (room && byRoom[room]) {
            byRoom[room].updates.push(update);
        }
    }

    // Group inserts by room
    for (const insert of mutations.inserts) {
        const room = insert.room;
        if (room && byRoom[room]) {
            byRoom[room].inserts.push(insert);
        }
    }

    // Group deletes - need to track which room they came from
    // For now, deletes go to all rooms (they'll be filtered by object existence)
    for (const deleteId of mutations.deletes) {
        for (const roomName of roomNames) {
            byRoom[roomName].deletes.push(deleteId);
        }
    }

    return byRoom;
}

/**
 * Finds which room an object belongs to
 * @param {string} objectId - Object ID
 * @param {any} mutations - All mutations
 * @returns {string|null}
 */
function findObjectRoom(objectId, mutations) {
    // Check if object was inserted (will have room property)
    const insert = mutations.inserts.find(i => i._id === objectId);
    if (insert && insert.room) {
        return insert.room;
    }

    // Check if any update has room property
    const update = mutations.updates.find(u => u.id === objectId && u.changes.room);
    if (update) {
        return update.changes.room;
    }

    return null;
}

/**
 * Computes final state for a specific room
 * @param {any} db - MongoDB database
 * @param {string} roomName - Room name
 * @param {any} mutations - Mutations for this room
 * @returns {Promise<any>}
 */
async function computeFinalStateForRoom(db, roomName, mutations) {
    const roomObjects = await loadRoomObjects(db, roomName);

    // Apply updates
    for (const update of mutations.updates) {
        if (roomObjects[update.id]) {
            Object.assign(roomObjects[update.id], update.changes);
        }
    }

    // Apply inserts
    for (const insert of mutations.inserts) {
        roomObjects[insert._id] = insert;
    }

    // Apply deletes
    for (const deleteId of mutations.deletes) {
        delete roomObjects[deleteId];
    }

    return roomObjects;
}

/**
 * Converts bulk updates to patch format (field-level changes)
 * Merges multiple updates for the same object into a single patch
 * @param {Array<{id: string, changes: any}>} updates
 * @returns {Array<any>}
 */
function convertUpdatesToPatchFormat(updates) {
    // Group updates by object ID and merge them
    const patchMap = new Map();

    for (const update of updates) {
        const existingPatch = patchMap.get(update.id);
        const newFields = flattenChanges(update.changes);

        if (existingPatch) {
            // Merge into existing patch (shallow merge - last write wins)
            Object.assign(existingPatch, newFields);
        } else {
            // Create new patch entry
            patchMap.set(update.id, {
                objectId: update.id,
                ...newFields
            });
        }
    }

    // Convert map to array
    return Array.from(patchMap.values());
}

/**
 * Flattens nested changes object to single-level object
 * @param {any} changes - Nested changes object
 * @returns {any}
 */
function flattenChanges(changes) {
    const flattened = {};

    for (const [key, value] of Object.entries(changes)) {
        if (typeof value === 'object' && value !== null && !Array.isArray(value)) {
            // Nested object - keep as-is (e.g., store: { energy: 50 })
            flattened[key] = value;
        } else {
            // Primitive or array - copy directly
            flattened[key] = value;
        }
    }

    return flattened;
}

/**
 * Extracts action logs from mutations (_actionLog field)
 * @param {any} mutations - Mutations from processor
 * @returns {any}
 */
function extractActionLogs(mutations) {
    const actionLogs = {};

    for (const update of mutations.updates) {
        if (update.changes._actionLog) {
            actionLogs[update.id] = update.changes._actionLog;
        }
    }

    return actionLogs;
}

/**
 * Computes final room state after applying mutations
 * @param {any} db - MongoDB database
 * @param {any} fixture - Original fixture
 * @param {any} mutations - Mutations from processor
 * @returns {Promise<any>}
 */
async function computeFinalState(db, fixture, mutations) {
    // Load current room objects
    const roomObjects = await loadRoomObjects(db, fixture.room);

    // Apply updates
    for (const update of mutations.updates) {
        if (roomObjects[update.id]) {
            Object.assign(roomObjects[update.id], update.changes);
        }
    }

    // Apply inserts
    for (const insert of mutations.inserts) {
        roomObjects[insert._id] = insert;
    }

    // Apply deletes
    for (const deleteId of mutations.deletes) {
        delete roomObjects[deleteId];
    }

    return roomObjects;
}

/**
 * Writes serialized output to JSON file
 * @param {any} output - Serialized output
 * @param {string} outputPath - Output file path
 */
function writeOutput(output, outputPath) {
    const fs = require('fs');
    const path = require('path');

    // Ensure output directory exists
    const dir = path.dirname(outputPath);
    if (!fs.existsSync(dir)) {
        fs.mkdirSync(dir, { recursive: true });
    }

    // Write JSON with pretty formatting
    fs.writeFileSync(outputPath, JSON.stringify(output, null, 2), 'utf8');
    console.log(`  ✓ Output written to ${outputPath}`);
}

module.exports = {
    serializeOutput,
    writeOutput
};
