const database = db.getSiblingDB("screeps");

const secondaryShardRoom = "W3N3";
const secondaryShardName = "shard1";

const rooms = database.getCollection("rooms");
rooms.updateOne(
  { _id: "W1N1" },
  {
    $set: {
      status: "normal",
      novice: false,
      respawnArea: false,
      openTime: null,
      owner: "TestUser",
      controller: { level: 3 },
      energyAvailable: 500
    }
  },
  { upsert: true }
);
rooms.updateOne(
  { _id: "W2N2" },
  {
    $set: {
      status: "out of borders",
      novice: false,
      respawnArea: false,
      openTime: null,
      owner: null,
      controller: { level: 0 },
      energyAvailable: 0
    }
  },
  { upsert: true }
);
rooms.updateOne(
  { _id: secondaryShardRoom },
  {
    $set: {
      status: "normal",
      shard: secondaryShardName,
      novice: false,
      respawnArea: false,
      openTime: null,
      owner: null,
      controller: { level: 1 },
      energyAvailable: 0
    }
  },
  { upsert: true }
);

const terrain = database.getCollection("rooms.terrain");
terrain.deleteMany({ room: { $in: ["W1N1", "W2N2", secondaryShardRoom] } });
terrain.insertOne({
  room: "W1N1",
  type: "terrain",
  terrain: Array(2500).fill("0").join("")
});
terrain.insertOne({
  room: "W2N2",
  type: "terrain",
  terrain: Array(2500).fill("1").join("")
});
terrain.insertOne({
  room: secondaryShardRoom,
  shard: secondaryShardName,
  type: "terrain",
  terrain: Array(2500).fill("2").join("")
});

const roomObjects = database.getCollection("rooms.objects");
roomObjects.deleteMany({ room: { $in: ["W1N1", "W2N2", secondaryShardRoom] } });
roomObjects.insertMany([
  {
    type: "controller",
    room: "W1N1",
    user: "test-user",
    level: 3,
    safeMode: 150000,
    sign: { user: "test-user", text: "Welcome!", time: 123456 }
  },
  {
    type: "mineral",
    room: "W1N1",
    mineralType: "H",
    density: 3
  },
  {
    type: "invaderCore",
    room: "W2N2",
    user: "Invader",
    level: 2
  },
  {
    type: "controller",
    room: secondaryShardRoom,
    shard: secondaryShardName,
    reservation: { user: "test-user", endTime: 150000 },
    sign: { user: "test-user", text: "Shard One Online", time: 123456 }
  },
  {
    type: "mineral",
    room: secondaryShardRoom,
    shard: secondaryShardName,
    mineralType: "O",
    density: 2
  }
]);

const worldInfo = database.getCollection("world.info");
worldInfo.updateOne(
  { _id: "world" },
  { $set: { gameTime: 123456, tickDuration: 500 } },
  { upsert: true }
);
