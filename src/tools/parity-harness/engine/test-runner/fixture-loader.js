const fs = require('fs');
const { MongoClient } = require('mongodb');

/**
 * Loads a JSON test fixture into MongoDB for execution by the Screeps engine.
 */

const MONGO_URL = process.env.MONGO_URL || 'mongodb://localhost:27017';
const DB_NAME = 'screeps-parity-test';

async function loadFixture(fixturePath) {
  const fixtureJson = fs.readFileSync(fixturePath, 'utf8');
  const fixture = JSON.parse(fixtureJson);

  if (!fixture.gameTime || !fixture.room || !fixture.shard) {
    throw new Error('Fixture must include gameTime, room, and shard');
  }

  console.log(`Loading fixture: ${fixturePath}`);
  console.log(`  Room: ${fixture.room}, GameTime: ${fixture.gameTime}, Shard: ${fixture.shard}`);

  const client = await MongoClient.connect(MONGO_URL, {
    useNewUrlParser: true,
    useUnifiedTopology: true
  });
  const db = client.db(DB_NAME);

  try {
    await db.collection('rooms.objects').deleteMany({ room: fixture.room });
    await db.collection('rooms').deleteMany({ _id: fixture.room });
    await db.collection('rooms.intents').deleteMany({ room: fixture.room });
    await db.collection('users').deleteMany({});

    await db.collection('rooms').insertOne({
      _id: fixture.room,
      status: 'normal',
      active: true,
      gameTime: fixture.gameTime
    });

    if (fixture.objects && fixture.objects.length > 0) {
      const roomObjects = fixture.objects.map(obj => ({ ...obj, room: fixture.room }));
      await db.collection('rooms.objects').insertMany(roomObjects);
      console.log(`  Loaded ${roomObjects.length} objects`);
    }

    if (fixture.intents && Object.keys(fixture.intents).length > 0) {
      await db.collection('rooms.intents').insertOne({ room: fixture.room, intents: fixture.intents });
      let intentCount = 0;
      for (const userId in fixture.intents) {
        for (const objectId in fixture.intents[userId]) {
          intentCount += fixture.intents[userId][objectId].length;
        }
      }
      console.log(`  Loaded ${intentCount} intents`);
    }

    if (fixture.users && Object.keys(fixture.users).length > 0) {
      for (const [userId, userData] of Object.entries(fixture.users)) {
        await db.collection('users').insertOne({ _id: userId, ...userData });
      }
      console.log(`  Loaded ${Object.keys(fixture.users).length} users`);
    }

    console.log('Fixture loaded successfully');
    return { client, db, fixture };
  } catch (error) {
    await client.close();
    throw error;
  }
}

async function cleanupFixture(client, fixture) {
  if (!client) return;
  try {
    const db = client.db(DB_NAME);
    await db.collection('rooms.objects').deleteMany({ room: fixture.room });
    await db.collection('rooms').deleteMany({ _id: fixture.room });
    await db.collection('rooms.intents').deleteMany({ room: fixture.room });
    await db.collection('users').deleteMany({});
  } catch (error) {
    console.error('Error cleaning up fixture:', error);
  } finally {
    await client.close();
  }
}

module.exports = { loadFixture, cleanupFixture, MONGO_URL, DB_NAME };
