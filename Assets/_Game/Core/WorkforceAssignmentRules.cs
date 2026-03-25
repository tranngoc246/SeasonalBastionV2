using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public static class WorkforceAssignmentRules
    {
        public static int CountAssignedToBuilding(IWorldState world, BuildingId buildingId, NpcId excludeNpc = default)
        {
            if (world?.Npcs == null || buildingId.Value == 0) return 0;

            int assigned = 0;
            foreach (var nid in world.Npcs.Ids)
            {
                if (!world.Npcs.Exists(nid)) continue;
                if (excludeNpc.Value != 0 && nid.Value == excludeNpc.Value) continue;

                var ns = world.Npcs.Get(nid);
                if (ns.Workplace.Value == buildingId.Value) assigned++;
            }

            return assigned;
        }

        public static int GetMaxAssignedFor(BuildingDef def, int level)
        {
            if (def == null) return 0;
            if (def.WorkRoles == WorkRoleFlags.None) return 0;
            if (def.IsHouse || def.IsTower) return 0;

            level = NormalizeLevel(level);

            if (def.IsHQ) return level switch { 1 => 2, 2 => 3, 3 => 4, _ => 2 };
            if (def.IsWarehouse) return level switch { 1 => 1, 2 => 2, 3 => 3, _ => 1 };

            if ((def.WorkRoles & WorkRoleFlags.Harvest) != 0) return level switch { 1 => 1, 2 => 2, 3 => 3, _ => 1 };
            if (def.IsForge || (def.WorkRoles & WorkRoleFlags.Craft) != 0) return level switch { 1 => 1, 2 => 2, 3 => 2, _ => 1 };
            if (def.IsArmory || (def.WorkRoles & WorkRoleFlags.Armory) != 0) return level switch { 1 => 1, 2 => 2, 3 => 2, _ => 1 };

            return 1;
        }

        public static bool CanAssignToTarget(IWorldState world, BuildingState targetState, BuildingDef targetDef, BuildingId targetId, NpcId npc, out string reason)
        {
            reason = string.Empty;

            if (world?.Buildings == null || world.Npcs == null)
            {
                reason = "WorldState missing.";
                return false;
            }

            if (targetId.Value == 0 || !world.Buildings.Exists(targetId))
            {
                reason = "Building not found.";
                return false;
            }

            if (!targetState.IsConstructed)
            {
                reason = "Công trình chưa xây xong: không thể assign.";
                return false;
            }

            if (targetDef == null)
            {
                reason = "Missing BuildingDef.";
                return false;
            }

            int max = GetMaxAssignedFor(targetDef, targetState.Level);
            if (max <= 0)
            {
                reason = "Building này không nhận worker.";
                return false;
            }

            int assigned = CountAssignedToBuilding(world, targetId, excludeNpc: npc);
            if (assigned >= max)
            {
                reason = $"Đã đủ worker ({assigned}/{max}).";
                return false;
            }

            return true;
        }

        public static int CountUnassigned(IWorldState world)
        {
            if (world?.Npcs == null) return 0;

            int count = 0;
            foreach (var nid in world.Npcs.Ids)
            {
                if (!world.Npcs.Exists(nid)) continue;
                if (world.Npcs.Get(nid).Workplace.Value == 0) count++;
            }
            return count;
        }

        public static int NormalizeLevel(int level)
        {
            if (level < 1) return 1;
            if (level > 3) return 3;
            return level;
        }
    }
}
