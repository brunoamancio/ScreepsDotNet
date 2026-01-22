/**
 * Serializes processor execution results to JSON for comparison with .NET output.
 */

async function serializeOutput(db, fixture, executionResult) {
  console.log('Serializing output...');

  const { mutations, stats, eventLog } = executionResult;

  // Query final room state from database
  const finalObjects = await db.collection('rooms.objects')
    .find({ room: fixture.room })
    .toArray();

  const finalState = {};
  finalObjects.forEach(obj => {
    finalState[obj._id] = obj;
  });

  // Build mutations in format matching .NET output
  const patches = mutations.updates.map(update => ({
    objectId: update.id,
    ...update.changes
  }));

  const upserts = mutations.inserts.map(obj => ({
    objectId: obj._id,
    type: obj.type,
    ...obj
  }));

  const removals = mutations.deletes.map(id => id);

  // Build action logs (extracted from _actionLog field if present)
  const actionLogs = {};
  finalObjects.forEach(obj => {
    if (obj._actionLog) {
      actionLogs[obj._id] = obj._actionLog;
    }
  });

  const output = {
    mutations: {
      patches,
      upserts,
      removals
    },
    stats,
    actionLogs,
    finalState
  };

  console.log(`  Serialized ${patches.length} patches, ${upserts.length} upserts, ${removals.length} removals`);
  console.log(`  Serialized ${Object.keys(stats).length} stats`);
  console.log(`  Serialized ${Object.keys(actionLogs).length} action logs`);
  console.log(`  Serialized ${finalObjects.length} final objects`);

  return output;
}

module.exports = { serializeOutput };
