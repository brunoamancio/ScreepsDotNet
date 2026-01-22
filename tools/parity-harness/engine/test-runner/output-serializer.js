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

    // Apply mutations to get final state
    const finalState = await computeFinalState(db, fixture, processorOutput.mutations);

    // Extract action logs from mutations
    const actionLogs = extractActionLogs(processorOutput.mutations);

    // Build output structure
    const output = {
        mutations: {
            patches: convertUpdatesToPatchFormat(processorOutput.mutations.updates),
            upserts: processorOutput.mutations.inserts,
            removals: processorOutput.mutations.deletes
        },
        stats: processorOutput.stats,
        actionLogs: actionLogs,
        finalState: finalState,
        metadata: {
            room: fixture.room,
            gameTime: fixture.gameTime,
            timestamp: new Date().toISOString()
        }
    };

    console.log(`  ✓ Serialized ${Object.keys(finalState).length} objects in final state`);

    return output;
}

/**
 * Converts bulk updates to patch format (field-level changes)
 * @param {Array<{id: string, changes: any}>} updates
 * @returns {Array<any>}
 */
function convertUpdatesToPatchFormat(updates) {
    const patches = [];

    for (const update of updates) {
        const patch = {
            objectId: update.id,
            ...flattenChanges(update.changes)
        };
        patches.push(patch);
    }

    return patches;
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
