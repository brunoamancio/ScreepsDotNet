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
            power: 0,
            powerExperimentations: 0,
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
