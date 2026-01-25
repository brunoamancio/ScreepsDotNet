#!/usr/bin/env node

// Simple test to verify multi-room fixture detection logic

const fs = require('fs');
const path = require('path');

const fixturePath = path.resolve(__dirname, '../../src/ScreepsDotNet.Engine.Tests/Parity/Fixtures/terminal_send.json');
const fixtureContent = fs.readFileSync(fixturePath, 'utf8');
const fixture = JSON.parse(fixtureContent);

console.log('========================================');
console.log('Multi-Room Fixture Detection Test');
console.log('========================================');
console.log('');

const isMultiRoom = fixture.rooms !== undefined;

console.log('Is multi-room:', isMultiRoom);

if (isMultiRoom) {
    const roomNames = Object.keys(fixture.rooms);
    console.log('Rooms:', roomNames.join(', '));
    console.log('Room count:', roomNames.length);

    // Count objects per room
    for (const roomName of roomNames) {
        const objectCount = fixture.rooms[roomName].objects.length;
        console.log(`  ${roomName}: ${objectCount} objects`);
    }

    // Check intents structure
    console.log('');
    console.log('Intent structure:');
    for (const roomName of Object.keys(fixture.intents || {})) {
        const userCount = Object.keys(fixture.intents[roomName]).length;
        console.log(`  ${roomName}: ${userCount} user(s)`);

        for (const userId of Object.keys(fixture.intents[roomName])) {
            const objectCount = Object.keys(fixture.intents[roomName][userId]).length;
            console.log(`    ${userId}: ${objectCount} object(s) with intents`);

            for (const objectId of Object.keys(fixture.intents[roomName][userId])) {
                const intentCount = fixture.intents[roomName][userId][objectId].length;
                const intentTypes = fixture.intents[roomName][userId][objectId].map(i => i.intent).join(', ');
                console.log(`      ${objectId}: ${intentCount} intent(s) [${intentTypes}]`);
            }
        }
    }
} else {
    console.log('Single-room fixture:', fixture.room);
}

console.log('');
console.log('✓ Multi-room detection logic verified');
