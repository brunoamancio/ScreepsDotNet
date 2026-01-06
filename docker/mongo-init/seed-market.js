const database = db.getSiblingDB("screeps");

const orders = database.getCollection("market.orders");
orders.deleteMany({});
orders.insertMany([
  {
    active: true,
    type: "sell",
    user: "test-user",
    roomName: "W1N1",
    resourceType: "energy",
    price: 5000,
    amount: 1000,
    remainingAmount: 600,
    totalAmount: 1000,
    created: 1000,
    createdTimestamp: Date.now()
  },
  {
    active: true,
    type: "buy",
    user: null,
    roomName: "W2N2",
    resourceType: "energy",
    price: 4500,
    amount: 800,
    remainingAmount: 800,
    totalAmount: 800,
    created: 1001,
    createdTimestamp: Date.now()
  }
]);

const stats = database.getCollection("market.stats");
stats.deleteMany({});
stats.insertMany([
  {
    resourceType: "energy",
    date: new Date().toISOString().slice(0, 10),
    transactions: 10,
    volume: 5000,
    avgPrice: 4.8,
    stddevPrice: 0.3
  },
  {
    resourceType: "energy",
    date: new Date(Date.now() - 86400000).toISOString().slice(0, 10),
    transactions: 5,
    volume: 2500,
    avgPrice: 4.5,
    stddevPrice: 0.25
  }
]);
