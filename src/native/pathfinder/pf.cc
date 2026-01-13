// Author: Marcel Laverdet <https://github.com/laverdet>
#include "pf.h"
#include <iostream>
#include <algorithm>
#include <stdexcept>
#include <cstring>

using namespace screeps;

template <typename T>
constexpr bool is_border_pos(T val) {
	return (val + 1) % 50 < 2;
}

template <typename T>
constexpr bool is_near_border_pos(T val) {
	return (val + 2) % 50 < 4;
}

uint8_t room_info_t::cost_matrix0[2500] = {0};

decltype(path_finder_t::terrain) path_finder_t::terrain = {{ nullptr }};
std::vector<std::unique_ptr<uint8_t[]>> path_finder_t::terrain_storage;
std::vector<std::unique_ptr<uint8_t[]>> path_finder_t::cost_matrix_storage;
room_callback_fn path_finder_t::native_room_callback = nullptr;
void* path_finder_t::native_room_callback_context = nullptr;

#if SCREEPS_PATHFINDER_HAS_V8
namespace {
	bool ShouldAbortForV8() {
		return v8::Isolate::GetCurrent()->IsExecutionTerminating();
	}
}
#endif

	// Return room index from a map position, allocates a new room index if needed and possible
	room_index_t path_finder_t::room_index_from_pos(const map_position_t map_pos) {
		room_index_t room_index = reverse_room_table[map_pos.id];
		if (room_index == 0) {
			if (room_table_size >= max_rooms) {
				return 0;
			}
			if (blocked_rooms.find(map_pos) != blocked_rooms.end()) {
				return 0;
			}
			uint8_t* terrain_ptr = terrain[map_pos.id];
			if (terrain_ptr == nullptr) {
#if SCREEPS_PATHFINDER_HAS_V8
				Nan::ThrowError("Could not load terrain data");
#endif
				throw js_error();
			}
			uint8_t* cost_matrix = nullptr;
#if SCREEPS_PATHFINDER_HAS_V8
			if (room_callback != nullptr) {
				Nan::TryCatch try_catch;
				v8::Local<v8::Value> argv[2];
				argv[0] = Nan::New(map_pos.xx);
				argv[1] = Nan::New(map_pos.yy);
				Nan::MaybeLocal<v8::Value> ret = Nan::Call(*room_callback, v8::Local<v8::Object>::Cast(Nan::Undefined()), 2, argv);
				if (try_catch.HasCaught()) {
					try_catch.ReThrow();
					throw js_error();
				}
				if (!ret.IsEmpty()) {
					v8::Local<v8::Value> ret_local = ret.ToLocalChecked();
					if (ret_local->IsBoolean() && ret_local->IsFalse()) {
						blocked_rooms.insert(map_pos);
						return 0;
					}
					room_data_handles[room_table_size] = ret_local;
					Nan::TypedArrayContents<uint8_t> cost_matrix_js(room_data_handles[room_table_size]);
					if (cost_matrix_js.length() == 2500) {
						cost_matrix = *cost_matrix_js;
					}
				}
			} else
#endif
			if (native_room_callback != nullptr) {
				room_callback_result result{};
				if (!native_room_callback(map_pos.xx, map_pos.yy, &result, native_room_callback_context)) {
					blocked_rooms.insert(map_pos);
					return 0;
				}

				if (result.block_room) {
					blocked_rooms.insert(map_pos);
					return 0;
				}

				if (result.cost_matrix != nullptr && result.cost_matrix_length >= 2500) {
					auto buffer = std::make_unique<uint8_t[]>(2500);
					std::memcpy(buffer.get(), result.cost_matrix, 2500);
					cost_matrix = buffer.get();
					cost_matrix_storage.push_back(std::move(buffer));
				}
			}
			room_table[room_table_size++] = room_info_t(terrain_ptr, cost_matrix, map_pos);
			return reverse_room_table[map_pos.id] = room_table_size;
		}
		return room_index;
	}

	// Conversions to/from index & world_position_t
	pos_index_t path_finder_t::index_from_pos(const world_position_t pos) {
		room_index_t room_index = room_index_from_pos(pos.map_position());
		if (room_index == 0) {
			throw std::runtime_error("Invalid invocation of index_from_pos");
		}
		return pos_index_t(room_index - 1) * 50 * 50 + pos.xx % 50 * 50 + pos.yy % 50;
	}

	world_position_t path_finder_t::pos_from_index(pos_index_t index) const {
		room_index_t room_index = index / (50 * 50);
		const room_info_t& terrain = room_table[room_index];
		unsigned int coord = index - room_index * 50 * 50;
		return world_position_t(coord / 50 + terrain.pos.xx * 50, coord % 50 + terrain.pos.yy * 50);
	}

	// Push a new node to the heap, or update its cost if it already exists
	void path_finder_t::push_node(pos_index_t parent_index, world_position_t node, cost_t g_cost) {
		pos_index_t index = index_from_pos(node);
		if (open_closed.is_closed(index)) {
			return;
		}
		cost_t h_cost = heuristic(node) * heuristic_weight;
		cost_t f_cost = h_cost + g_cost;

		if (open_closed.is_open(index)) {
			if (heap.priority(index) > f_cost) {
				heap.update(index, f_cost);
				parents[index] = parent_index;
				// std::cout <<"~ " <<node <<": h(" <<h_cost <<") + " <<"g(" <<g_cost <<") = f(" <<f_cost <<")\n";
			}
		} else {
			heap.insert(index, f_cost);
			open_closed.open(index);
			parents[index] = parent_index;
			// std::cout <<"+ " <<node <<": h(" <<h_cost <<") + " <<"g(" <<g_cost <<") = f(" <<f_cost <<")\n";
		}
	}

	// Return cost of moving to a node
	cost_t path_finder_t::look(const world_position_t pos) {
		room_index_t room_index = room_index_from_pos(pos.map_position());
		if (room_index == 0) {
			return obstacle;
		}
		const room_info_t& terrain = room_table[room_index - 1];
		if (terrain.cost_matrix != nullptr) {
			int tmp = terrain.cost_matrix[pos.xx % 50][pos.yy % 50];
			if (tmp != 0) {
				if (tmp == 0xff) {
					return obstacle;
				} else {
					return tmp;
				}
			}
		}
		return look_table[terrain.look(pos.xx % 50, pos.yy % 50)];
	}

	// Returns the minimum Chebyshev distance to a goal
	cost_t path_finder_t::heuristic(const world_position_t pos) const {
		if (flee) {
			cost_t ret = 0;
			for (size_t ii = 0; ii < goals.size(); ++ii) {
				cost_t dist = pos.range_to(goals[ii].pos);
				if (dist < goals[ii].range) {
					ret = std::max<cost_t>(ret, goals[ii].range - dist);
				}
			}
			return ret;
		} else {
			cost_t ret = std::numeric_limits<cost_t>::max();
			for (size_t ii = 0; ii < goals.size(); ++ii) {
				cost_t dist = pos.range_to(goals[ii].pos);
				if (dist > goals[ii].range) {
					ret = std::min<cost_t>(ret, dist - goals[ii].range);
				} else {
					ret = 0;
				}
			}
			return ret;
		}
	}

	// Run an iteration of basic A*
	void path_finder_t::astar(pos_index_t index, world_position_t pos, cost_t g_cost) {
		for (int dir = world_position_t::TOP; dir <= world_position_t::TOP_LEFT; ++dir) {
			world_position_t neighbor = pos.position_in_direction(static_cast<world_position_t::direction_t>(dir));

			// If this is a portal node there are some moves which will be impossible, and should be discarded
			if (pos.xx % 50 == 0) {
				if (neighbor.xx % 50 == 49 && pos.yy != neighbor.yy) {
					continue;
				} else if (pos.xx == neighbor.xx) {
					continue;
				}
			} else if (pos.xx % 50 == 49) {
				if (neighbor.xx % 50 == 0 && pos.yy != neighbor.yy) {
					continue;
				} else if (pos.xx == neighbor.xx) {
					continue;
				}
			} else if (pos.yy % 50 == 0) {
				if (neighbor.yy % 50 == 49 && pos.xx != neighbor.xx) {
					continue;
				} else if (pos.yy == neighbor.yy) {
					continue;
				}
			} else if (pos.yy % 50 == 49) {
				if (neighbor.yy % 50 == 0 && pos.xx != neighbor.xx) {
					continue;
				} else if (pos.yy == neighbor.yy) {
					continue;
				}
			}

			// Calculate cost of this move
			cost_t n_cost = look(neighbor);
			if (n_cost == obstacle) {
				// std::cout <<"# " <<neighbor <<"\n";
				continue;
			}
			push_node(index, neighbor, g_cost + n_cost);
		}
	}

	// JPS dragons
	world_position_t path_finder_t::jump_x(cost_t cost, world_position_t pos, int dx) {
		cost_t prev_cost_u = look(world_position_t(pos.xx, pos.yy - 1));
		cost_t prev_cost_d = look(world_position_t(pos.xx, pos.yy + 1));
		while (true) {
			if (heuristic(pos) == 0 || is_near_border_pos(pos.xx)) {
				break;
			}

			cost_t cost_u = look(world_position_t(pos.xx + dx, pos.yy - 1));
			cost_t cost_d = look(world_position_t(pos.xx + dx, pos.yy + 1));
			if (
				(cost_u != obstacle && prev_cost_u != cost) ||
				(cost_d != obstacle && prev_cost_d != cost)
			) {
				break;
			}
			prev_cost_u = cost_u;
			prev_cost_d = cost_d;
			pos.xx += dx;

			cost_t jump_cost = look(pos);
			if (jump_cost == obstacle) {
				pos = world_position_t::null();
				break;
			} else if (jump_cost != cost) {
				break;
			}
		}
		return pos;
	}

	world_position_t path_finder_t::jump_y(cost_t cost, world_position_t pos, int dy) {
		cost_t prev_cost_l = look(world_position_t(pos.xx - 1, pos.yy));
		cost_t prev_cost_r = look(world_position_t(pos.xx + 1, pos.yy));
		while (true) {
			if (heuristic(pos) == 0 || is_near_border_pos(pos.yy)) {
				break;
			}

			cost_t cost_l = look(world_position_t(pos.xx - 1, pos.yy + dy));
			cost_t cost_r = look(world_position_t(pos.xx + 1, pos.yy + dy));
			if (
				(cost_l != obstacle && prev_cost_l != cost) ||
				(cost_r != obstacle && prev_cost_r != cost)
			) {
				break;
			}
			prev_cost_l = cost_l;
			prev_cost_r = cost_r;
			pos.yy += dy;

			cost_t jump_cost = look(pos);
			if (jump_cost == obstacle) {
				pos = world_position_t::null();
				break;
			} else if (jump_cost != cost) {
				break;
			}
		}
		return pos;
	}

	world_position_t path_finder_t::jump_xy(cost_t cost, world_position_t pos, int dx, int dy) {
		cost_t prev_cost_x = look(world_position_t(pos.xx - dx, pos.yy));
		cost_t prev_cost_y = look(world_position_t(pos.xx, pos.yy - dy));
		while (true) {
			if (heuristic(pos) == 0 || is_near_border_pos(pos.xx) || is_near_border_pos(pos.yy)) {
				break;
			}

			if (
				(look(world_position_t(pos.xx - dx, pos.yy + dy)) != obstacle && prev_cost_x != cost) ||
				(look(world_position_t(pos.xx + dx, pos.yy - dy)) != obstacle && prev_cost_y != cost)
			) {
				break;
			}
			prev_cost_x = look(world_position_t(pos.xx, pos.yy + dy));
			prev_cost_y = look(world_position_t(pos.xx + dx, pos.yy));
			if (
				(prev_cost_y != obstacle && !jump_x(cost, world_position_t(pos.xx + dx, pos.yy), dx).is_null()) ||
				(prev_cost_x != obstacle && !jump_y(cost, world_position_t(pos.xx, pos.yy + dy), dy).is_null())
			) {
				break;
			}

			pos.xx += dx;
			pos.yy += dy;

			cost_t jump_cost = look(pos);
			if (jump_cost == obstacle) {
				pos = world_position_t::null();
				break;
			} else if (jump_cost != cost) {
				break;
			}
		}
		return pos;
	}

	world_position_t path_finder_t::jump(cost_t cost, world_position_t pos, int dx, int dy) {
		if (dx != 0) {
			if (dy != 0) {
				return jump_xy(cost, pos, dx, dy);
			} else {
				return jump_x(cost, pos, dx);
			}
		} else {
			return jump_y(cost, pos, dy);
		}
	}

	void path_finder_t::jps(pos_index_t index, world_position_t pos, cost_t g_cost) {
		world_position_t parent = pos_from_index(parents[index]);
		int dx = pos.xx > parent.xx ? 1 : (pos.xx < parent.xx ? -1 : 0);
		int dy = pos.yy > parent.yy ? 1 : (pos.yy < parent.yy ? -1 : 0);

		// First check to see if we're jumping to/from a border, options are limited in this case
		world_position_t neighbors[3];
		int neighbor_count = 0;
		if (pos.xx % 50 == 0) {
			if (dx == -1) {
				neighbors[0] = world_position_t(pos.xx - 1, pos.yy);
				neighbor_count = 1;
			} else if (dx == 1) {
				neighbors[0] = world_position_t(pos.xx + 1, pos.yy - 1);
				neighbors[1] = world_position_t(pos.xx + 1, pos.yy);
				neighbors[2] = world_position_t(pos.xx + 1, pos.yy + 1);
				neighbor_count = 3;
			}
		} else if (pos.xx % 50 == 49) {
			if (dx == 1) {
				neighbors[0] = world_position_t(pos.xx + 1, pos.yy);
				neighbor_count = 1;
			} else if (dx == -1) {
				neighbors[0] = world_position_t(pos.xx - 1, pos.yy - 1);
				neighbors[1] = world_position_t(pos.xx - 1, pos.yy);
				neighbors[2] = world_position_t(pos.xx - 1, pos.yy + 1);
				neighbor_count = 3;
			}
		} else if (pos.yy % 50 == 0) {
			if (dy == -1) {
				neighbors[0] = world_position_t(pos.xx, pos.yy - 1);
				neighbor_count = 1;
			} else if (dy == 1) {
				neighbors[0] = world_position_t(pos.xx - 1, pos.yy + 1);
				neighbors[1] = world_position_t(pos.xx, pos.yy + 1);
				neighbors[2] = world_position_t(pos.xx + 1, pos.yy + 1);
				neighbor_count = 3;
			}
		} else if (pos.yy % 50 == 49) {
			if (dy == 1) {
				neighbors[0] = world_position_t(pos.xx, pos.yy + 1);
				neighbor_count = 1;
			} else if (dy == -1) {
				neighbors[0] = world_position_t(pos.xx - 1, pos.yy - 1);
				neighbors[1] = world_position_t(pos.xx, pos.yy - 1);
				neighbors[2] = world_position_t(pos.xx + 1, pos.yy - 1);
				neighbor_count = 3;
			}
		}

		// Add special nodes from the above blocks to the heap
		if (neighbor_count != 0) {
			for (int ii = 0; ii < neighbor_count; ++ii) {
				cost_t n_cost = look(neighbors[ii]);
				if (n_cost == obstacle) {
					continue;
				}
				push_node(index, neighbors[ii], g_cost + n_cost);
			}
			return;
		}

		// Regular JPS iteration follows

		// First check to see if we're close to borders
		int border_dx = 0;
		if (pos.xx % 50 == 1) {
			border_dx = -1;
		} else if (pos.xx % 50 == 48) {
			border_dx = 1;
		}
		int border_dy = 0;
		if (pos.yy % 50 == 1) {
			border_dy = -1;
		} else if (pos.yy % 50 == 48) {
			border_dy = 1;
		}

		// Now execute the logic that is shared between diagonal and straight jumps
		cost_t cost = look(pos);
		if (dx != 0) {
			world_position_t neighbor = world_position_t(pos.xx + dx, pos.yy);
			cost_t n_cost = look(neighbor);
			if (n_cost != obstacle) {
				if (border_dy == 0) {
					jump_neighbor(pos, index, neighbor, g_cost, cost, n_cost);
				} else {
					push_node(index, neighbor, g_cost + n_cost);
				}
			}
		}
		if (dy != 0) {
			world_position_t neighbor = world_position_t(pos.xx, pos.yy + dy);
			cost_t n_cost = look(neighbor);
			if (n_cost != obstacle) {
				if (border_dx == 0) {
					jump_neighbor(pos, index, neighbor, g_cost, cost, n_cost);
				} else {
					push_node(index, neighbor, g_cost + n_cost);
				}
			}
		}

		// Forced neighbor rules
		if (dx != 0) {
			if (dy != 0) { // Jumping diagonally
				world_position_t neighbor = world_position_t(pos.xx + dx, pos.yy + dy);
				cost_t n_cost = look(neighbor);
				if (n_cost != obstacle) {
					jump_neighbor(pos, index, neighbor, g_cost, cost, n_cost);
				}
				if (look(world_position_t(pos.xx - dx, pos.yy)) != cost) {
					jump_neighbor(pos, index, world_position_t(pos.xx - dx, pos.yy + dy), g_cost, cost, look(world_position_t(pos.xx - dx, pos.yy + dy)));
				}
				if (look(world_position_t(pos.xx, pos.yy - dy)) != cost) {
					jump_neighbor(pos, index, world_position_t(pos.xx + dx, pos.yy - dy), g_cost, cost, look(world_position_t(pos.xx + dx, pos.yy - dy)));
				}
			} else { // Jumping left / right
				if (border_dy == 1 || look(world_position_t(pos.xx, pos.yy + 1)) != cost) {
					jump_neighbor(pos, index, world_position_t(pos.xx + dx, pos.yy + 1), g_cost, cost, look(world_position_t(pos.xx + dx, pos.yy + 1)));
				}
				if (border_dy == -1 || look(world_position_t(pos.xx, pos.yy - 1)) != cost) {
					jump_neighbor(pos, index, world_position_t(pos.xx + dx, pos.yy - 1), g_cost, cost, look(world_position_t(pos.xx + dx, pos.yy - 1)));
				}
			}
		} else { // Jumping up / down
			if (border_dx == 1 || look(world_position_t(pos.xx + 1, pos.yy)) != cost) {
				jump_neighbor(pos, index, world_position_t(pos.xx + 1, pos.yy + dy), g_cost, cost, look(world_position_t(pos.xx + 1, pos.yy + dy)));
			}
			if (border_dx == -1 || look(world_position_t(pos.xx - 1, pos.yy)) != cost) {
				jump_neighbor(pos, index, world_position_t(pos.xx - 1, pos.yy + dy), g_cost, cost, look(world_position_t(pos.xx - 1, pos.yy + dy)));
			}
		}
	}

	void path_finder_t::jump_neighbor(world_position_t pos, pos_index_t index, world_position_t neighbor, cost_t g_cost, cost_t cost, cost_t n_cost) {
		if (n_cost != cost || is_border_pos(neighbor.xx) || is_border_pos(neighbor.yy)) {
			if (n_cost == obstacle) {
				return;
			}
			g_cost += n_cost;
		} else {
			neighbor = jump(n_cost, neighbor, neighbor.xx - pos.xx, neighbor.yy - pos.yy);
			if (neighbor.is_null()) {
				return;
			}
			g_cost += n_cost * (pos.range_to(neighbor) - 1) + look(neighbor);
		}

		push_node(index, neighbor, g_cost);
	}

	search_status path_finder_t::search_native(
		const search_request_native& request,
		search_result_native& result,
		abort_callback_fn should_abort
	) {

		for (size_t ii = 0; ii < room_table_size; ++ii) {
			reverse_room_table[room_table[ii].pos.id] = 0;
		}
		room_table_size = 0;
		blocked_rooms.clear();
		goals.clear();
		open_closed.clear();
		heap.clear();
		cost_matrix_storage.clear();

		result.path.clear();
		result.operations = 0;
		result.cost = 0;
		result.incomplete = false;
		result.status = search_status::Error;

		goals.reserve(request.goal_count);
		for (size_t ii = 0; ii < request.goal_count; ++ii) {
			goals.push_back(request.goals[ii]);
		}

		look_table[0] = request.options.plain_cost;
		look_table[2] = request.options.swamp_cost;
		this->max_rooms = request.options.max_rooms;
		this->heuristic_weight = request.options.heuristic_weight;
		uint32_t ops_remaining = request.options.max_ops;
		this->flee = request.options.flee;
		world_position_t origin = request.origin;
		cost_t min_node_h_cost = std::numeric_limits<cost_t>::max();
		cost_t min_node_g_cost = std::numeric_limits<cost_t>::max();
		pos_index_t min_node = 0;

		if (heuristic(origin) == 0) {
			result.status = search_status::SamePosition;
			return result.status;
		}

		_is_in_use = true;
		try {
			if (room_index_from_pos(origin.map_position()) == 0) {
				_is_in_use = false;
				result.status = search_status::InvalidStart;
				return result.status;
			}

			min_node = index_from_pos(origin);
			astar(min_node, origin, 0);

			while (!heap.empty() && ops_remaining > 0) {
				std::pair<pos_index_t, cost_t> current = heap.pop();
				open_closed.close(current.first);

				world_position_t pos = pos_from_index(current.first);
				cost_t h_cost = heuristic(pos);
				cost_t g_cost = current.second - cost_t(h_cost * heuristic_weight);

				if (h_cost == 0) {
					min_node = current.first;
					min_node_h_cost = 0;
					min_node_g_cost = g_cost;
					break;
				} else if (h_cost < min_node_h_cost) {
					min_node = current.first;
					min_node_h_cost = h_cost;
					min_node_g_cost = g_cost;
				}
				if (g_cost + h_cost > request.options.max_cost) {
					break;
				}

				jps(current.first, pos, g_cost);
				--ops_remaining;

				if (should_abort != nullptr && should_abort()) {
					_is_in_use = false;
					result.status = search_status::Interrupted;
					return result.status;
				}
			}
		} catch (js_error&) {
			_is_in_use = false;
			result.status = search_status::Error;
			return result.status;
		}

		std::vector<world_position_t> reconstructed;
		pos_index_t index = min_node;
		world_position_t pos = pos_from_index(index);
		while (pos != origin) {
			reconstructed.push_back(pos);
			index = parents[index];
			world_position_t next = pos_from_index(index);
			if (next.range_to(pos) > 1) {
				world_position_t::direction_t dir = pos.direction_to(next);
				do {
					pos = pos.position_in_direction(dir);
					reconstructed.push_back(pos);
				} while (pos.range_to(next) > 1);
			}
			pos = next;
		}

		result.path = std::move(reconstructed);
		result.operations = request.options.max_ops - ops_remaining;
		result.cost = min_node_g_cost;
		result.incomplete = (min_node_h_cost != 0);
		result.status = search_status::Success;
		_is_in_use = false;
		return result.status;
	}

#if SCREEPS_PATHFINDER_HAS_V8
	v8::Local<v8::Value> path_finder_t::search(
		v8::Local<v8::Value> origin_js,
		v8::Local<v8::Array> goals_js,
		v8::Local<v8::Function> room_callback,
		cost_t plain_cost,
		cost_t swamp_cost,
		uint8_t max_rooms,
		uint32_t max_ops,
		uint32_t max_cost,
		bool flee,
		double heuristic_weight
	) {

		std::vector<goal_t> goal_buffer;
		goal_buffer.reserve(goals_js->Length());
		for (uint32_t ii = 0; ii < goals_js->Length(); ++ii) {
			goal_buffer.push_back(goal_t(Nan::Get(goals_js, ii).ToLocalChecked()));
		}

		v8::Local<v8::Value> room_data_handle_holder[k_max_rooms];
		room_data_handles = room_data_handle_holder;
		if (room_callback->IsUndefined()) {
			this->room_callback = nullptr;
		} else {
			this->room_callback = &room_callback;
		}

		world_position_t origin(origin_js);
		search_request_native request{
			origin,
			goal_buffer.data(),
			goal_buffer.size(),
			search_options_native{
				plain_cost,
				swamp_cost,
				max_rooms,
				max_ops,
				max_cost,
				flee,
				heuristic_weight
			}
		};

		search_result_native native_result;
		search_status status = search_native(request, native_result, ShouldAbortForV8);

		this->room_callback = nullptr;
		room_data_handles = nullptr;

		if (status == search_status::SamePosition) {
			return Nan::Undefined();
		}
		if (status == search_status::InvalidStart) {
			return Nan::New(-1);
		}
		if (status != search_status::Success) {
			return Nan::Undefined();
		}

		v8::Local<v8::Array> path = Nan::New<v8::Array>(static_cast<uint32_t>(native_result.path.size()));
		for (uint32_t ii = 0; ii < native_result.path.size(); ++ii) {
			const auto& node = native_result.path[ii];
			v8::Local<v8::Array> tmp = Nan::New<v8::Array>(2);
			Nan::Set(tmp, 0, Nan::New(node.xx));
			Nan::Set(tmp, 1, Nan::New(node.yy));
			Nan::Set(path, ii, tmp);
		}

		v8::Local<v8::Object> ret = Nan::New<v8::Object>();
		Nan::Set(ret, Nan::New("path").ToLocalChecked(), path);
		Nan::Set(ret, Nan::New("ops").ToLocalChecked(), Nan::New(native_result.operations));
		Nan::Set(ret, Nan::New("cost").ToLocalChecked(), Nan::New(native_result.cost));
		Nan::Set(ret, Nan::New("incomplete").ToLocalChecked(), Nan::New<v8::Boolean>(native_result.incomplete));
		return ret;
	}
#endif

	void path_finder_t::reset_terrain_storage() {
		std::fill(terrain.begin(), terrain.end(), nullptr);
		terrain_storage.clear();
	}

	void path_finder_t::ingest_terrain_chunk(map_position_t pos, const uint8_t* source, size_t length) {
		if (source == nullptr || length < terrain_bytes_per_room)
			return;

		auto buffer = std::make_unique<uint8_t[]>(terrain_bytes_per_room);
		std::memcpy(buffer.get(), source, terrain_bytes_per_room);
		terrain[pos.id] = buffer.get();
		terrain_storage.push_back(std::move(buffer));
	}

#if SCREEPS_PATHFINDER_HAS_V8
	// Loads static terrain data into module upfront (legacy V8 path)
	void path_finder_t::load_terrain(v8::Local<v8::Array> terrain_array) {
		if (terrain_array.IsEmpty())
			return;

		reset_terrain_storage();
		for (uint32_t ii = 0; ii < terrain_array->Length(); ++ii) {
			v8::Local<v8::Object> terrain_info = Nan::To<v8::Object>(Nan::Get(terrain_array, ii).ToLocalChecked()).ToLocalChecked();
			map_position_t pos = Nan::Get(terrain_info, Nan::New("room").ToLocalChecked()).ToLocalChecked();
			v8::Local<v8::Value> bits_value = Nan::Get(terrain_info, Nan::New("bits").ToLocalChecked()).ToLocalChecked();
			Nan::TypedArrayContents<uint8_t> bits(bits_value);
			if (bits.length() >= terrain_bytes_per_room)
				ingest_terrain_chunk(pos, *bits, terrain_bytes_per_room);
		}
	}
#endif

	// Loads terrain data from POD structs (native bridge)
	void path_finder_t::load_terrain(const terrain_room_plain* rooms, size_t count) {
		if (rooms == nullptr || count == 0)
			return;

		reset_terrain_storage();
		for (size_t ii = 0; ii < count; ++ii) {
			const auto& room = rooms[ii];
			map_position_t pos(room.xx, room.yy);
			ingest_terrain_chunk(pos, room.bits, room.length);
		}
	}

	void path_finder_t::set_room_callback(room_callback_fn callback, void* userData)
	{
		native_room_callback = callback;
		native_room_callback_context = userData;
	}
