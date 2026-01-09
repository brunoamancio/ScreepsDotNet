db = db.getSiblingDB('screeps');

db.users.updateOne(
    { _id: 'test-user' },
    {
        $set: {
            username: 'TestUser',
            email: 'test@screeps.local',
            emailDirty: false,
            active: 1,
            cpu: 100,
            bot: null,
            password: 'hashed-password',
            money: 100000,
            badge: null,
            customBadge: null,
            notifyPrefs: {},
            gcl: { level: 1, progress: 0, progressTotal: 1 },
            lastRespawnDate: new Date(),
            lastChargeTime: new Date(),
            blocked: false,
            power: 1000000,
            powerExperimentations: 2,
            powerExperimentationTime: 0,
            steam: {
                id: '90071992547409920',
                displayName: 'Test Player',
                ownership: null,
                steamProfileLinkHidden: false
            }
        }
    },
    { upsert: true }
);

db['rooms.objects'].updateOne(
    { user: 'test-user', type: 'controller', room: 'W1N1' },
    {
        $set: {
            level: 3,
            user: 'test-user',
            type: 'controller',
            room: 'W1N1'
        }
    },
    { upsert: true }
);

db['rooms.objects'].updateOne(
    { user: 'test-user', type: 'controller', room: 'W2N2' },
    {
        $set: {
            level: 5,
            user: 'test-user',
            type: 'controller',
            room: 'W2N2'
        }
    },
    { upsert: true }
);

db['rooms.objects'].updateOne(
    { user: 'test-user', type: 'spawn', room: 'W1N1' },
    {
        $set: {
            name: 'Spawn1',
            user: 'test-user',
            type: 'spawn',
            room: 'W1N1'
        }
    },
    { upsert: true }
);

db.users.updateOne(
    { _id: 'ally-user' },
    {
        $set: {
            username: 'AllyUser',
            usernameLower: 'allyuser',
            email: 'ally@screeps.local',
            emailDirty: false,
            active: 1,
            cpu: 100,
            bot: null,
            password: 'hashed-password',
            notifyPrefs: {},
            gcl: { level: 1, progress: 0, progressTotal: 1 },
            lastRespawnDate: new Date(),
            lastChargeTime: new Date(),
            blocked: false,
            power: 100000,
            badge: null,
            customBadge: null
        }
    },
    { upsert: true }
);

const powerCreepActiveId = ObjectId('64d000000000000000000001');
const powerCreepDormantId = ObjectId('64d000000000000000000002');

db['rooms.objects'].updateOne(
    { _id: powerCreepActiveId },
    {
        $set: {
            _id: powerCreepActiveId,
            user: 'test-user',
            type: 'powerCreep',
            room: 'W1N1',
            x: 20,
            y: 20,
            hits: 2800,
            hitsMax: 3000,
            ticksToLive: 4500,
            store: { ops: 120 },
            storeCapacity: 400,
            fatigue: 0
        }
    },
    { upsert: true }
);

db['users.power_creeps'].deleteMany({ user: 'test-user' });
db['users.power_creeps'].insertMany([
    {
        _id: powerCreepActiveId,
        user: 'test-user',
        name: 'IntegrationOperator',
        className: 'operator',
        level: 3,
        hitsMax: 3000,
        store: { ops: 120 },
        storeCapacity: 400,
        spawnCooldownTime: null,
        powers: {
            '1': { level: 2 },
            '2': { level: 1 }
        }
    },
    {
        _id: powerCreepDormantId,
        user: 'test-user',
        name: 'BenchOperator',
        className: 'operator',
        level: 1,
        hitsMax: 2000,
        store: {},
        storeCapacity: 200,
        spawnCooldownTime: 0,
        powers: {
            '1': { level: 1 }
        }
    }
]);

db['users.money'].deleteMany({ user: 'test-user' });
db['users.money'].insertMany([
    {
        user: 'test-user',
        type: 'market.sell',
        change: 5000,
        balance: 15000,
        description: 'Sold energy on the market',
        date: new Date()
    },
    {
        user: 'test-user',
        type: 'market.buy',
        change: -2000,
        balance: 10000,
        description: 'Purchased minerals',
        date: new Date(Date.now() - 3600 * 1000)
    }
]);

const messageOutId = new ObjectId();
const now = new Date();

db['users.messages'].deleteMany({});
db['users.messages'].insertMany([
    {
        _id: messageOutId,
        user: 'ally-user',
        respondent: 'test-user',
        date: now,
        type: 'out',
        text: 'Welcome to the Screeps.NET rewrite!',
        unread: false
    },
    {
        user: 'test-user',
        respondent: 'ally-user',
        date: now,
        type: 'in',
        text: 'Welcome to the Screeps.NET rewrite!',
        unread: true,
        outMessage: messageOutId
    }
]);

db['users.notifications'].deleteMany({});
db['users.notifications'].insertOne({
    user: 'test-user',
    message: 'New message from AllyUser',
    date: Date.now(),
    type: 'msg',
    count: 1
});
