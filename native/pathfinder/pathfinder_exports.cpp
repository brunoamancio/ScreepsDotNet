#define SCREEPS_PATHFINDER_EXPORTS

#include "pathfinder_exports.h"
#include <cstring>

extern "C"
{
    int ScreepsPathfinder_LoadTerrain(const ScreepsTerrainRoom* rooms, int count)
    {
        (void)rooms;
        (void)count;
        return -1;
    }

    int ScreepsPathfinder_Search(
        const ScreepsPathfinderPoint* origin,
        const ScreepsPathfinderGoal* goals,
        int goalCount,
        const ScreepsPathfinderOptionsNative* options,
        ScreepsPathfinderResultNative* result)
    {
        (void)origin;
        (void)goals;
        (void)goalCount;
        (void)options;
        if (result)
        {
            result->path = nullptr;
            result->pathLength = 0;
            result->operations = 0;
            result->cost = 0;
            result->incomplete = true;
        }
        return -1;
    }

    void ScreepsPathfinder_FreeResult(ScreepsPathfinderResultNative* result)
    {
        if (result == nullptr || result->path == nullptr)
            return;

        delete[] result->path;
        result->path = nullptr;
        result->pathLength = 0;
    }
}
