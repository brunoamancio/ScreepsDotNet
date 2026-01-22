#!/usr/bin/env node

/**
 * CLI wrapper for running Screeps engine parity fixtures.
 * 
 * Usage:
 *   node run-fixture.js path/to/fixture.json --output output.json
 */

const fs = require('fs');
const path = require('path');
const { loadFixture, cleanupFixture } = require('./fixture-loader');
const { executeProcessor } = require('./processor-executor');
const { serializeOutput } = require('./output-serializer');

async function main() {
  const args = process.argv.slice(2);
  
  if (args.length === 0) {
    console.error('Usage: node run-fixture.js <fixture.json> [--output output.json]');
    process.exit(1);
  }

  const fixturePath = args[0];
  let outputPath = null;

  // Parse --output flag
  const outputIndex = args.indexOf('--output');
  if (outputIndex !== -1 && args[outputIndex + 1]) {
    outputPath = args[outputIndex + 1];
  }

  if (!fs.existsSync(fixturePath)) {
    console.error(`Error: Fixture file not found: ${fixturePath}`);
    process.exit(1);
  }

  console.log('=== Screeps Parity Harness ===');
  console.log('');

  let client = null;
  let fixture = null;

  try {
    // Step 1: Load fixture into MongoDB
    const loadResult = await loadFixture(fixturePath);
    client = loadResult.client;
    const db = loadResult.db;
    fixture = loadResult.fixture;

    console.log('');

    // Step 2: Execute processor
    const executionResult = await executeProcessor(db, fixture);

    console.log('');

    // Step 3: Serialize output
    const output = await serializeOutput(db, fixture, executionResult);

    console.log('');

    // Step 4: Write output to file
    if (outputPath) {
      fs.writeFileSync(outputPath, JSON.stringify(output, null, 2));
      console.log(`Output written to: ${outputPath}`);
    } else {
      console.log('Output:');
      console.log(JSON.stringify(output, null, 2));
    }

    console.log('');
    console.log('=== Execution Complete ===');
  } catch (error) {
    console.error('');
    console.error('=== Execution Failed ===');
    console.error('Error:', error.message);
    console.error(error.stack);
    process.exit(1);
  } finally {
    // Cleanup
    if (client && fixture) {
      await cleanupFixture(client, fixture);
    }
  }
}

main().catch(error => {
  console.error('Unhandled error:', error);
  process.exit(1);
});
