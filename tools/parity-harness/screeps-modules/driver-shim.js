/**
 * Minimal driver shim for parity testing
 * Provides constants from @screeps/common without loading native modules
 */

const path = require('path');
const common = require(path.join(__dirname, 'common'));

// Export only what the processors need
exports.constants = common.configManager.config.common.constants;

// Simple pathfinder mock for NPC AI (straight-line paths)
// This matches the .NET implementation's simple movement calculation
exports.pathFinder = {
    search: function(origin, goal, opts) {
        // Simple straight-line path from origin to goal
        // This matches the .NET Engine's simple movement calculation for NPC AI
        const path = [];
        let currentX = origin.x;
        let currentY = origin.y;

        // Handle goal as either a position or an object with pos/range
        let targetX, targetY, targetRange = 0;
        if (goal.pos) {
            targetX = goal.pos.x;
            targetY = goal.pos.y;
            targetRange = goal.range || 0;
        } else if (Array.isArray(goal)) {
            // Multiple goals - use first one
            if (goal.length === 0) {
                return { path: [], ops: 0, cost: 0, incomplete: false };
            }
            targetX = goal[0].pos.x;
            targetY = goal[0].pos.y;
            targetRange = goal[0].range || 0;
        } else {
            targetX = goal.x;
            targetY = goal.y;
        }

        // Calculate straight-line path
        const maxSteps = 50; // Prevent infinite loops
        let steps = 0;
        while (steps < maxSteps) {
            const dx = targetX - currentX;
            const dy = targetY - currentY;
            const distance = Math.max(Math.abs(dx), Math.abs(dy));

            // Stop if within target range
            if (distance <= targetRange) {
                break;
            }

            // Calculate next step (move toward target)
            const stepX = dx === 0 ? 0 : (dx > 0 ? 1 : -1);
            const stepY = dy === 0 ? 0 : (dy > 0 ? 1 : -1);

            currentX += stepX;
            currentY += stepY;

            // Add position to path (fake RoomPosition object)
            path.push({
                x: currentX,
                y: currentY,
                roomName: origin.roomName,
                getRangeTo: function(pos) {
                    return Math.max(Math.abs(this.x - pos.x), Math.abs(this.y - pos.y));
                },
                sPackLocal: function() {
                    // Pack position into single character (6 bits X, 6 bits Y)
                    let uint32 = 0;
                    uint32 <<= 6; uint32 |= this.x;
                    uint32 <<= 6; uint32 |= this.y;
                    return String.fromCharCode(32 + uint32);
                }
            });

            steps++;
        }

        return {
            path: path,
            ops: path.length,
            cost: path.length,
            incomplete: false
        };
    }
};

exports.config = common.configManager.config;
