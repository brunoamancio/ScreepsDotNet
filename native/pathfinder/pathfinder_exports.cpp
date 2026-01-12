#define SCREEPS_PATHFINDER_EXPORTS

#include "pathfinder_exports.h"
#include "pf.h"
#include <cctype>
#include <cstring>
#include <vector>

namespace
{
    bool ParseRoomName(const char* name, uint8_t& xx, uint8_t& yy)
    {
        if (name == nullptr || *name == '\0')
            return false;

        auto clamp = [](int value) {
            if (value < 0) return 0;
            if (value > 127) return 127;
            return value;
        };

        size_t len = std::strlen(name);
        if (len < 4)
            return false;

        size_t index = 0;
        char horizontalAxis = static_cast<char>(std::toupper(static_cast<unsigned char>(name[index++])));

        int horizontalValue = 0;
        while (index < len && std::isdigit(static_cast<unsigned char>(name[index])))
        {
            horizontalValue = horizontalValue * 10 + (name[index] - '0');
            index++;
        }
        if (index >= len)
            return false;

        char verticalAxis = static_cast<char>(std::toupper(static_cast<unsigned char>(name[index++])));
        int verticalValue = 0;
        while (index < len && std::isdigit(static_cast<unsigned char>(name[index])))
        {
            verticalValue = verticalValue * 10 + (name[index] - '0');
            index++;
        }

        horizontalValue = clamp(horizontalValue);
        verticalValue = clamp(verticalValue);

        auto convert_axis = [](char axis, int value, bool horizontal) -> int {
            switch (axis)
            {
                case 'W':
                    return 127 - value;
                case 'E':
                    return 128 + value;
                case 'N':
                    return 127 - value;
                case 'S':
                    return 128 + value;
                default:
                    return -1;
            }
        };

        int horizontalCoord = convert_axis(horizontalAxis, horizontalValue, true);
        int verticalCoord = convert_axis(verticalAxis, verticalValue, false);
        if (horizontalCoord < 0 || verticalCoord < 0 || horizontalCoord > 255 || verticalCoord > 255)
            return false;

        xx = static_cast<uint8_t>(horizontalCoord);
        yy = static_cast<uint8_t>(verticalCoord);
        return true;
    }
}

extern "C"
{
    int ScreepsPathfinder_LoadTerrain(const ScreepsTerrainRoom* rooms, int count)
    {
        if (rooms == nullptr || count <= 0)
            return -1;

        std::vector<screeps::terrain_room_plain> entries;
        entries.reserve(count);

        for (int i = 0; i < count; ++i)
        {
            const auto& room = rooms[i];
            if (room.terrainBytes == nullptr || room.terrainLength < static_cast<int>(screeps::k_terrain_bytes))
                continue;

            uint8_t xx = 0;
            uint8_t yy = 0;
            if (!ParseRoomName(room.roomName, xx, yy))
                continue;

            screeps::terrain_room_plain plain{
                xx,
                yy,
                room.terrainBytes,
                static_cast<size_t>(room.terrainLength)
            };
            entries.push_back(plain);
        }

        if (entries.empty())
            return -2;

        screeps::path_finder_t::load_terrain(entries.data(), entries.size());
        return 0;
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
