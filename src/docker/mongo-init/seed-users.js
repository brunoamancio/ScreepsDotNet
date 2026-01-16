db = db.getSiblingDB('screeps');

const Collections = {
    Users: 'users',
    RoomsObjects: 'rooms.objects',
    UserPowerCreeps: 'users.power_creeps',
    UserMoney: 'users.money',
    UserMessages: 'users.messages',
    UserNotifications: 'users.notifications'
};

const UserFields = {
    Id: '_id',
    Username: 'username',
    UsernameLower: 'usernameLower',
    Email: 'email',
    EmailDirty: 'emailDirty',
    Active: 'active',
    Cpu: 'cpu',
    Bot: 'bot',
    Password: 'password',
    Money: 'money',
    Badge: 'badge',
    CustomBadge: 'customBadge',
    NotifyPrefs: 'notifyPrefs',
    Gcl: 'gcl',
    LastRespawnDate: 'lastRespawnDate',
    LastChargeTime: 'lastChargeTime',
    Blocked: 'blocked',
    Power: 'power',
    PowerExperimentations: 'powerExperimentations',
    PowerExperimentationTime: 'powerExperimentationTime',
    Steam: 'steam'
};

const RoomObjectFields = {
    Id: '_id',
    Type: 'type',
    Room: 'room',
    User: 'user',
    Name: 'name',
    X: 'x',
    Y: 'y',
    Hits: 'hits',
    HitsMax: 'hitsMax',
    TicksToLive: 'ticksToLive',
    Store: 'store',
    StoreCapacity: 'storeCapacity',
    Fatigue: 'fatigue',
    Level: 'level'
};

const MessageFields = {
    Id: '_id',
    User: 'user',
    Respondent: 'respondent',
    Date: 'date',
    Type: 'type',
    Text: 'text',
    Unread: 'unread',
    OutMessageId: 'outMessage'
};

const MoneyEntryFields = {
    User: 'user',
    Type: 'type',
    Change: 'change',
    Balance: 'balance',
    Description: 'description',
    Date: 'date'
};

const PowerCreepFields = {
    Id: '_id',
    User: 'user',
    Name: 'name',
    ClassName: 'className',
    Level: 'level',
    HitsMax: 'hitsMax',
    Store: 'store',
    StoreCapacity: 'storeCapacity',
    SpawnCooldownTime: 'spawnCooldownTime',
    Powers: 'powers'
};

const NotificationFields = {
    User: 'user',
    Message: 'message',
    Date: 'date',
    Type: 'type',
    Count: 'count'
};

const NotificationTypeValues = {
    Message: 'msg'
};

function upsertUserDocument(userId, values) {
    db[Collections.Users].updateOne(
        { [UserFields.Id]: userId },
        { $set: values },
        { upsert: true }
    );
}

function upsertRoomObject(filter, values) {
    db[Collections.RoomsObjects].updateOne(filter, { $set: values }, { upsert: true });
}

upsertUserDocument('test-user', {
    [UserFields.Username]: 'TestUser',
    [UserFields.UsernameLower]: 'testuser',
    [UserFields.Email]: 'test@screeps.local',
    [UserFields.EmailDirty]: false,
    [UserFields.Active]: 1,
    [UserFields.Cpu]: 100,
    [UserFields.Bot]: null,
    [UserFields.Password]: 'hashed-password',
    [UserFields.Money]: 100000,
    [UserFields.Badge]: null,
    [UserFields.CustomBadge]: null,
    [UserFields.NotifyPrefs]: {},
    [UserFields.Gcl]: { level: 1, progress: 0, progressTotal: 1 },
    [UserFields.LastRespawnDate]: new Date(),
    [UserFields.LastChargeTime]: new Date(),
    [UserFields.Blocked]: false,
    [UserFields.Power]: 1000000,
    [UserFields.PowerExperimentations]: 2,
    [UserFields.PowerExperimentationTime]: 0,
    [UserFields.Steam]: {
        id: '90071992547409920',
        displayName: 'Test Player',
        ownership: null,
        steamProfileLinkHidden: false
    }
});

upsertRoomObject(
    {
        [RoomObjectFields.User]: 'test-user',
        [RoomObjectFields.Type]: 'controller',
        [RoomObjectFields.Room]: 'W1N1'
    },
    {
        [RoomObjectFields.Level]: 3,
        [RoomObjectFields.User]: 'test-user',
        [RoomObjectFields.Type]: 'controller',
        [RoomObjectFields.Room]: 'W1N1'
    }
);

upsertRoomObject(
    {
        [RoomObjectFields.User]: 'test-user',
        [RoomObjectFields.Type]: 'controller',
        [RoomObjectFields.Room]: 'W2N2'
    },
    {
        [RoomObjectFields.Level]: 5,
        [RoomObjectFields.User]: 'test-user',
        [RoomObjectFields.Type]: 'controller',
        [RoomObjectFields.Room]: 'W2N2'
    }
);

upsertRoomObject(
    {
        [RoomObjectFields.User]: 'test-user',
        [RoomObjectFields.Type]: 'spawn',
        [RoomObjectFields.Room]: 'W1N1'
    },
    {
        [RoomObjectFields.Name]: 'Spawn1',
        [RoomObjectFields.User]: 'test-user',
        [RoomObjectFields.Type]: 'spawn',
        [RoomObjectFields.Room]: 'W1N1'
    }
);

upsertUserDocument('ally-user', {
    [UserFields.Username]: 'AllyUser',
    [UserFields.UsernameLower]: 'allyuser',
    [UserFields.Email]: 'ally@screeps.local',
    [UserFields.EmailDirty]: false,
    [UserFields.Active]: 1,
    [UserFields.Cpu]: 100,
    [UserFields.Bot]: null,
    [UserFields.Password]: 'hashed-password',
    [UserFields.NotifyPrefs]: {},
    [UserFields.Gcl]: { level: 1, progress: 0, progressTotal: 1 },
    [UserFields.LastRespawnDate]: new Date(),
    [UserFields.LastChargeTime]: new Date(),
    [UserFields.Blocked]: false,
    [UserFields.Power]: 100000,
    [UserFields.Badge]: null,
    [UserFields.CustomBadge]: null
});

const powerCreepActiveId = ObjectId('64d000000000000000000001');
const powerCreepDormantId = ObjectId('64d000000000000000000002');

upsertRoomObject(
    { [RoomObjectFields.Id]: powerCreepActiveId },
    {
        [RoomObjectFields.Id]: powerCreepActiveId,
        [RoomObjectFields.User]: 'test-user',
        [RoomObjectFields.Type]: 'powerCreep',
        [RoomObjectFields.Room]: 'W1N1',
        [RoomObjectFields.X]: 20,
        [RoomObjectFields.Y]: 20,
        [RoomObjectFields.Hits]: 2800,
        [RoomObjectFields.HitsMax]: 3000,
        [RoomObjectFields.TicksToLive]: 4500,
        [RoomObjectFields.Store]: { ops: 120 },
        [RoomObjectFields.StoreCapacity]: 400,
        [RoomObjectFields.Fatigue]: 0
    }
);

db[Collections.UserPowerCreeps].deleteMany({ [PowerCreepFields.User]: 'test-user' });
db[Collections.UserPowerCreeps].insertMany([
    {
        [PowerCreepFields.Id]: powerCreepActiveId,
        [PowerCreepFields.User]: 'test-user',
        [PowerCreepFields.Name]: 'IntegrationOperator',
        [PowerCreepFields.ClassName]: 'operator',
        [PowerCreepFields.Level]: 3,
        [PowerCreepFields.HitsMax]: 3000,
        [PowerCreepFields.Store]: { ops: 120 },
        [PowerCreepFields.StoreCapacity]: 400,
        [PowerCreepFields.SpawnCooldownTime]: null,
        [PowerCreepFields.Powers]: {
            '1': { level: 2 },
            '2': { level: 1 }
        }
    },
    {
        [PowerCreepFields.Id]: powerCreepDormantId,
        [PowerCreepFields.User]: 'test-user',
        [PowerCreepFields.Name]: 'BenchOperator',
        [PowerCreepFields.ClassName]: 'operator',
        [PowerCreepFields.Level]: 1,
        [PowerCreepFields.HitsMax]: 2000,
        [PowerCreepFields.Store]: {},
        [PowerCreepFields.StoreCapacity]: 200,
        [PowerCreepFields.SpawnCooldownTime]: 0,
        [PowerCreepFields.Powers]: {
            '1': { level: 1 }
        }
    }
]);

db[Collections.UserMoney].deleteMany({ [MoneyEntryFields.User]: 'test-user' });
db[Collections.UserMoney].insertMany([
    {
        [MoneyEntryFields.User]: 'test-user',
        [MoneyEntryFields.Type]: 'market.sell',
        [MoneyEntryFields.Change]: 5000,
        [MoneyEntryFields.Balance]: 15000,
        [MoneyEntryFields.Description]: 'Sold energy on the market',
        [MoneyEntryFields.Date]: new Date()
    },
    {
        [MoneyEntryFields.User]: 'test-user',
        [MoneyEntryFields.Type]: 'market.buy',
        [MoneyEntryFields.Change]: -2000,
        [MoneyEntryFields.Balance]: 10000,
        [MoneyEntryFields.Description]: 'Purchased minerals',
        [MoneyEntryFields.Date]: new Date(Date.now() - 3600 * 1000)
    }
]);

const messageOutId = new ObjectId();
const now = new Date();

db[Collections.UserMessages].deleteMany({});
db[Collections.UserMessages].insertMany([
    {
        [MessageFields.Id]: messageOutId,
        [MessageFields.User]: 'ally-user',
        [MessageFields.Respondent]: 'test-user',
        [MessageFields.Date]: now,
        [MessageFields.Type]: 'out',
        [MessageFields.Text]: 'Welcome to the Screeps.NET rewrite!',
        [MessageFields.Unread]: false
    },
    {
        [MessageFields.User]: 'test-user',
        [MessageFields.Respondent]: 'ally-user',
        [MessageFields.Date]: now,
        [MessageFields.Type]: 'in',
        [MessageFields.Text]: 'Welcome to the Screeps.NET rewrite!',
        [MessageFields.Unread]: true,
        [MessageFields.OutMessageId]: messageOutId
    }
]);

db[Collections.UserNotifications].deleteMany({});
db[Collections.UserNotifications].insertOne({
    [NotificationFields.User]: 'test-user',
    [NotificationFields.Message]: 'New message from AllyUser',
    [NotificationFields.Date]: Date.now(),
    [NotificationFields.Type]: NotificationTypeValues.Message,
    [NotificationFields.Count]: 1
});
