const database = db.getSiblingDB("screeps");

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

const terrain = database.getCollection("rooms.terrain");
terrain.deleteMany({ room: { $in: ["W1N1"] } });
terrain.insertOne({
  room: "W1N1",
  type: "terrain",
  terrain: Array(2500).fill("0").join("")
});
