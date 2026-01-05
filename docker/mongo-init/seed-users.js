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
