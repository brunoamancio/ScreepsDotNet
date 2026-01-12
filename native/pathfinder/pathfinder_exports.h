#pragma once

#include <cstdint>

#ifdef _WIN32
#ifdef SCREEPS_PATHFINDER_EXPORTS
#define SCREEPS_PATHFINDER_API __declspec(dllexport)
#else
#define SCREEPS_PATHFINDER_API __declspec(dllimport)
#endif
#else
#define SCREEPS_PATHFINDER_API __attribute__((visibility("default")))
#endif

extern "C"
{
    struct ScreepsTerrainRoom
    {
        const char* roomName;
        const uint8_t* terrainBytes;
        int terrainLength;
    };

    struct ScreepsPathfinderGoal
    {
        int targetX;
        int targetY;
        const char* roomName;
        int range;
    };

    struct ScreepsPathfinderOptionsNative
    {
        bool flee;
        int maxRooms;
        int maxOps;
        int maxCost;
        int plainCost;
        int swampCost;
        double heuristicWeight;
    };

    struct ScreepsPathfinderPoint
    {
        int x;
        int y;
        char roomName[8];
    };

    struct ScreepsPathfinderResultNative
    {
        ScreepsPathfinderPoint* path;
        int pathLength;
        int operations;
        int cost;
        bool incomplete;
    };

    typedef bool (*ScreepsRoomCallback)(
        uint8_t roomX,
        uint8_t roomY,
        const uint8_t** costMatrix,
        int* costMatrixLength,
        void* userData);

    SCREEPS_PATHFINDER_API int ScreepsPathfinder_LoadTerrain(const ScreepsTerrainRoom* rooms, int count);
    SCREEPS_PATHFINDER_API int ScreepsPathfinder_Search(
        const ScreepsPathfinderPoint* origin,
        const ScreepsPathfinderGoal* goals,
        int goalCount,
        const ScreepsPathfinderOptionsNative* options,
        ScreepsPathfinderResultNative* result);
    SCREEPS_PATHFINDER_API void ScreepsPathfinder_FreeResult(ScreepsPathfinderResultNative* result);
    SCREEPS_PATHFINDER_API void ScreepsPathfinder_SetRoomCallback(ScreepsRoomCallback callback, void* userData);
}
