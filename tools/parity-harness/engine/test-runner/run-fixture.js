#!/usr/bin/env node

const path = require('path');
const { loadFixture } = require('./fixture-loader');
const { executeProcessor } = require('./processor-executor');
const { serializeOutput, writeOutput } = require('./output-serializer');

/**
 * Main CLI entry point
 */
async function main() {
    const args = process.argv.slice(2);

    if (args.length === 0 || args.includes('--help') || args.includes('-h')) {
        printUsage();
        process.exit(0);
    }

    // Parse arguments
    const fixturePath = args[0];
    const mongoUrl = getArgValue(args, '--mongo', 'mongodb://localhost:27017');
    const outputPath = getArgValue(args, '--output', null);

    if (!fixturePath) {
        console.error('ERROR: Fixture path is required');
        printUsage();
        process.exit(1);
    }

    console.log('========================================');
    console.log('Screeps Parity Test - Node.js Runner');
    console.log('========================================');
    console.log(`Fixture: ${fixturePath}`);
    console.log(`MongoDB: ${mongoUrl}`);
    console.log(`Output:  ${outputPath || '(stdout)'}`);
    console.log('');

    let client = null;

    try {
        // Step 1: Load fixture into MongoDB
        const { client: dbClient, db, fixture } = await loadFixture(fixturePath, mongoUrl);
        client = dbClient;

        // Step 2: Execute processor
        const processorOutput = await executeProcessor(db, fixture);

        // Step 3: Serialize output
        const output = await serializeOutput(db, fixture, processorOutput);

        // Step 4: Write output
        if (outputPath) {
            writeOutput(output, outputPath);
        } else {
            console.log('\n========================================');
            console.log('Output (JSON):');
            console.log('========================================');
            console.log(JSON.stringify(output, null, 2));
        }

        console.log('\n✓ Fixture execution complete');
        process.exit(0);

    } catch (error) {
        console.error('\n========================================');
        console.error('ERROR:');
        console.error('========================================');
        console.error(error.stack || error.message);
        process.exit(1);

    } finally {
        if (client) {
            await client.close();
        }
    }
}

/**
 * Gets argument value by key
 * @param {string[]} args - CLI arguments
 * @param {string} key - Argument key (e.g., '--output')
 * @param {string|null} defaultValue - Default value if not found
 * @returns {string|null}
 */
function getArgValue(args, key, defaultValue = null) {
    const index = args.indexOf(key);
    if (index !== -1 && index + 1 < args.length) {
        return args[index + 1];
    }
    return defaultValue;
}

/**
 * Prints CLI usage information
 */
function printUsage() {
    console.log('');
    console.log('Usage: node run-fixture.js <fixture-path> [options]');
    console.log('');
    console.log('Arguments:');
    console.log('  <fixture-path>        Path to JSON fixture file');
    console.log('');
    console.log('Options:');
    console.log('  --output <path>       Write output to file (default: stdout)');
    console.log('  --mongo <url>         MongoDB connection URL (default: mongodb://localhost:27017)');
    console.log('  --help, -h            Show this help message');
    console.log('');
    console.log('Examples:');
    console.log('  node run-fixture.js fixtures/harvest_basic.json');
    console.log('  node run-fixture.js fixtures/harvest_basic.json --output output.json');
    console.log('  node run-fixture.js fixtures/harvest_basic.json --mongo mongodb://host:port');
    console.log('');
}

// Run main function
if (require.main === module) {
    main().catch(error => {
        console.error('Fatal error:', error);
        process.exit(1);
    });
}

module.exports = { main };
