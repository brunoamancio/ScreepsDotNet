db = db.getSiblingDB('screeps');

db.serverData.updateOne(
    { _id: 'serverInfo' },
    {
        $set: {
            name: 'ScreepsDotNet Dev',
            build: '0.1.0-alpha',
            cliEnabled: true
        }
    },
    { upsert: true }
);
