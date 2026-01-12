#define SCREEPS_PATHFINDER_EXPORTS

#include "pathfinder_exports.h"
#include "pf.h"
#include <algorithm>
#include <cctype>
#include <cstdio>
#include <cstring>
#include <limits>
#include <new>
#include <vector>

namespace
{
    ScreepsRoomCallback g_room_callback = nullptr;
    void* g_room_user_data = nullptr;

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

    void FormatRoomName(uint8_t xx, uint8_t yy, char* buffer, size_t capacity)
    {
        if (buffer == nullptr || capacity == 0)
            return;

        char horizontalAxis = xx <= 127 ? 'W' : 'E';
        int horizontalValue = xx <= 127 ? 127 - xx : xx - 128;
        char verticalAxis = yy <= 127 ? 'N' : 'S';
        int verticalValue = yy <= 127 ? 127 - yy : yy - 128;
        std::snprintf(buffer, capacity, "%c%d%c%d", horizontalAxis, horizontalValue, verticalAxis, verticalValue);
    }

    bool ToWorldPosition(int x, int y, const char* roomName, screeps::world_position_t& dest)
    {
        if (roomName == nullptr)
            return false;
        if (x < 0 || x >= 50 || y < 0 || y >= 50)
            return false;

        uint8_t roomX = 0;
        uint8_t roomY = 0;
        if (!ParseRoomName(roomName, roomX, roomY))
            return false;

        uint32_t worldX = static_cast<uint32_t>(roomX) * 50u + static_cast<uint32_t>(x);
        uint32_t worldY = static_cast<uint32_t>(roomY) * 50u + static_cast<uint32_t>(y);
        dest = screeps::world_position_t(worldX, worldY);
        return true;
    }

    bool RoomCallbackBridge(uint8_t roomX, uint8_t roomY, screeps::room_callback_result* result, void*)
    {
        if (g_room_callback == nullptr)
        {
            if (result != nullptr)
            {
                result->cost_matrix = nullptr;
                result->cost_matrix_length = 0;
            }
            return true;
        }

        const uint8_t* costMatrix = nullptr;
        int length = 0;
        if (!g_room_callback(roomX, roomY, &costMatrix, &length, g_room_user_data))
        {
            if (result != nullptr)
            {
                result->cost_matrix = nullptr;
                result->cost_matrix_length = 0;
            }
            return false;
        }

        if (result != nullptr)
        {
            result->cost_matrix = costMatrix;
            result->cost_matrix_length = length > 0 ? static_cast<size_t>(length) : 0;
        }

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
        if (origin == nullptr || result == nullptr || goalCount < 0)
            return -1;
        if (goalCount > 0 && goals == nullptr)
            return -1;

        result->path = nullptr;
        result->pathLength = 0;
        result->operations = 0;
        result->cost = 0;
        result->incomplete = true;

        screeps::world_position_t originWorld;
        if (!ToWorldPosition(origin->x, origin->y, origin->roomName, originWorld))
            return -1;

        std::vector<screeps::goal_t> goalBuffer;
        goalBuffer.reserve(static_cast<size_t>(std::max(0, goalCount)));
        for (int ii = 0; ii < goalCount; ++ii)
        {
            screeps::world_position_t goalPos;
            if (!ToWorldPosition(goals[ii].targetX, goals[ii].targetY, goals[ii].roomName, goalPos))
                return -1;

            int rangeValue = goals[ii].range;
            if (rangeValue < 0)
                rangeValue = 0;
            auto clampedRange = static_cast<screeps::cost_t>(
                std::min<uint64_t>(static_cast<uint64_t>(rangeValue), std::numeric_limits<screeps::cost_t>::max()));
            goalBuffer.emplace_back(goalPos, clampedRange);
        }

        const screeps::search_options_native opts{
            static_cast<screeps::cost_t>(options != nullptr ? std::max(options->plainCost, 1) : 1),
            static_cast<screeps::cost_t>(options != nullptr ? std::max(options->swampCost, 1) : 5),
            static_cast<uint8_t>(options != nullptr ? std::clamp(options->maxRooms, 1, static_cast<int>(screeps::k_max_rooms)) : 16),
            static_cast<uint32_t>(options != nullptr ? std::max(options->maxOps, 1) : 20000),
            static_cast<uint32_t>(options != nullptr && options->maxCost > 0 ? options->maxCost : std::numeric_limits<uint32_t>::max()),
            options != nullptr ? options->flee : false,
            options != nullptr ? options->heuristicWeight : 1.2
        };

        static screeps::path_finder_t pathfinder;
        if (pathfinder.is_in_use())
            return -5;

        screeps::search_request_native request{
            originWorld,
            goalBuffer.empty() ? nullptr : goalBuffer.data(),
            goalBuffer.size(),
            opts
        };

        screeps::search_result_native nativeResult;
        screeps::search_status status = pathfinder.search_native(request, nativeResult);
        if (status == screeps::search_status::InvalidStart)
            return -2;
        if (status == screeps::search_status::Interrupted)
            return -3;
        if (status == screeps::search_status::Error)
            return -4;

        const size_t pathLength = nativeResult.path.size();
        if (pathLength > 0)
        {
            result->path = new (std::nothrow) ScreepsPathfinderPoint[pathLength];
            if (result->path == nullptr)
                return -4;

            for (size_t ii = 0; ii < pathLength; ++ii)
            {
                const auto& node = nativeResult.path[ii];
                ScreepsPathfinderPoint& point = result->path[ii];
                point.x = static_cast<int>(node.xx % 50);
                point.y = static_cast<int>(node.yy % 50);
                screeps::map_position_t room = node.map_position();
                FormatRoomName(room.xx, room.yy, point.roomName, sizeof(point.roomName));
            }
        }

        result->pathLength = static_cast<int>(pathLength);
        result->operations = static_cast<int>(nativeResult.operations);
        result->cost = static_cast<int>(nativeResult.cost);
        result->incomplete = nativeResult.incomplete;
        return 0;
    }

    void ScreepsPathfinder_FreeResult(ScreepsPathfinderResultNative* result)
    {
        if (result == nullptr || result->path == nullptr)
            return;

        delete[] result->path;
        result->path = nullptr;
        result->pathLength = 0;
    }

    void ScreepsPathfinder_SetRoomCallback(ScreepsRoomCallback callback, void* userData)
    {
        g_room_callback = callback;
        g_room_user_data = userData;
        if (callback == nullptr)
        {
            screeps::path_finder_t::set_room_callback(nullptr, nullptr);
            return;
        }

        screeps::path_finder_t::set_room_callback(RoomCallbackBridge, nullptr);
    }
}
