const database = db.getSiblingDB('screeps');
const collection = database.getCollection('server.data');

collection.deleteMany({ _id: 'server-info' });

collection.insertOne({
  _id: 'server-info',
  welcomeText: '<h4>Welcome to ScreepsDotNet!</h4>Use mods or Mongo updates to change this message.',
  customObjectTypes: {},
  historyChunkSize: 20,
  socketUpdateThrottle: 100,
  renderer: {
    resources: {},
    metadata: {}
  }
});

const versionCollection = database.getCollection('server.version');
versionCollection.deleteMany({ _id: 'version-info' });
versionCollection.insertOne({
  _id: 'version-info',
  protocol: 14,
  useNativeAuth: false,
  packageVersion: '0.0.1-dev'
});
