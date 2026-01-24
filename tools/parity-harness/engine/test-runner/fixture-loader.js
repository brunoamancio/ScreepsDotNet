const fs = require('fs');
const { MongoClient } = require('mongodb');

/**
 * Loads a JSON fixture into MongoDB test database
 * @param {string} fixturePath - Path to fixture JSON file
 * @param {string} mongoUrl - MongoDB connection URL
 * @returns {Promise<{client: MongoClient, db: any, fixture: any}>}
 */
async function loadFixture(fixturePath, mongoUrl = 'mongodb://localhost:27017') {
    console.log(`Loading fixture: ${fixturePath}`);

    // Parse fixture JSON
    const fixtureContent = fs.readFileSync(fixturePath, 'utf8');
    const fixture = JSON.parse(fixtureContent);

    // Validate fixture structure
    validateFixture(fixture);

    // Connect to MongoDB
    const client = await MongoClient.connect(mongoUrl, {
        useNewUrlParser: true,
        useUnifiedTopology: true
    });

    const db = client.db('screeps-parity-test');

    console.log('  Clearing existing data...');

    // Clear collections for this room
    await db.collection('rooms.objects').deleteMany({ room: fixture.room });
    await db.collection('rooms.intents').deleteMany({ room: fixture.room });
    await db.collection('rooms.terrain').deleteMany({ room: fixture.room });
    await db.collection('rooms').deleteMany({ _id: fixture.room });

    // Clear user data
    const userIds = Object.keys(fixture.users || {});
    if (userIds.length > 0) {
        await db.collection('users').deleteMany({ _id: { $in: userIds } });
    }

    console.log('  Inserting fixture data...');

    // Insert room objects
    if (fixture.objects && fixture.objects.length > 0) {
        const roomObjects = fixture.objects.map(obj => ({
            ...obj,
            room: fixture.room
        }));
        await db.collection('rooms.objects').insertMany(roomObjects);
        console.log(`    ✓ Inserted ${roomObjects.length} room objects`);
    }

    // Insert room metadata
    await db.collection('rooms').insertOne({
        _id: fixture.room,
        status: 'normal',
        active: true
    });

    // Insert terrain
    if (fixture.terrain) {
        await db.collection('rooms.terrain').insertOne({
            _id: fixture.room,
            room: fixture.room,
            terrain: fixture.terrain,
            type: 'terrain'
        });
        console.log(`    ✓ Inserted terrain`);
    }

    // Insert users
    for (const [userId, userData] of Object.entries(fixture.users || {})) {
        await db.collection('users').insertOne({
            _id: userId,
            ...userData
        });
    }
    if (userIds.length > 0) {
        console.log(`    ✓ Inserted ${userIds.length} users`);
    }

    // Store intents in format expected by processor
    if (fixture.intents) {
        const intentCount = Object.values(fixture.intents).reduce((sum, userIntents) => {
            return sum + Object.values(userIntents).reduce((userSum, objIntents) => {
                return userSum + objIntents.length;
            }, 0);
        }, 0);

        await db.collection('rooms.intents').insertOne({
            room: fixture.room,
            intents: fixture.intents
        });
        console.log(`    ✓ Inserted ${intentCount} intents`);
    }

    console.log('  ✓ Fixture loaded successfully');

    return { client, db, fixture };
}

/**
 * Validates fixture structure
 * @param {any} fixture - Parsed fixture object
 */
function validateFixture(fixture) {
    if (!fixture.room) {
        throw new Error('Fixture missing required field: room');
    }
    if (typeof fixture.gameTime !== 'number') {
        throw new Error('Fixture missing required field: gameTime (must be number)');
    }
    if (!fixture.objects || !Array.isArray(fixture.objects)) {
        throw new Error('Fixture missing required field: objects (must be array)');
    }
    if (!fixture.users || typeof fixture.users !== 'object') {
        throw new Error('Fixture missing required field: users (must be object)');
    }
}

/**
 * Loads room objects from MongoDB
 * @param {any} db - MongoDB database
 * @param {string} roomName - Room name
 * @returns {Promise<{[id: string]: any}>}
 */
async function loadRoomObjects(db, roomName) {
    const objects = await db.collection('rooms.objects')
        .find({ room: roomName })
        .toArray();

    const objectMap = {};
    for (const obj of objects) {
        objectMap[obj._id] = obj;
    }

    return objectMap;
}

/**
 * Loads room terrain from MongoDB
 * @param {any} db - MongoDB database
 * @param {string} roomName - Room name
 * @returns {Promise<any>}
 */
async function loadRoomTerrain(db, roomName) {
    const terrain = await db.collection('rooms.terrain')
        .findOne({ room: roomName });
    return terrain ? terrain.terrain : null;
}

module.exports = {
    loadFixture,
    loadRoomObjects,
    loadRoomTerrain
};
