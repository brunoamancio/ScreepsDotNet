#!/usr/bin/env node
const path = require('path');
const { spawnSync } = require('child_process');
const fs = require('fs');

function resolveRepoRoot(startDir) {
  for (let dir = startDir; ; dir = path.dirname(dir)) {
    if (fs.existsSync(path.join(dir, '.git'))) {
      return dir;
    }
    if (dir === path.dirname(dir)) {
      throw new Error('Unable to locate repository root (no .git directory found).');
    }
  }
}

const scriptDir = __dirname;
const repoRoot = resolveRepoRoot(scriptDir);
const reportScript = path.join(repoRoot, 'src', 'native', 'pathfinder', 'scripts', 'run-legacy-regressions.js');
const baselinePath = path.join(repoRoot, 'src', 'ScreepsDotNet.Driver.Tests', 'Pathfinding', 'Baselines', 'legacy-regressions.json');
const expectationPath = baselinePath;

const result = spawnSync(process.execPath, [reportScript, '--baseline', baselinePath, '--expect', expectationPath], {
  cwd: repoRoot,
  stdio: 'inherit'
});

process.exit(result.status || 0);
