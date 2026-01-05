db = db.getSiblingDB('screeps');

db.users.updateOne(
    { _id: 'test-user' },
    {
        $set: {
            username: 'TestUser',
            active: 1,
            cpu: 100,
            bot: null
        }
    },
    { upsert: true }
);
