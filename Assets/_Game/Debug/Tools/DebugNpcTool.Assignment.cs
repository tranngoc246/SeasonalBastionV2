using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SeasonalBastion.DebugTools
{
    public sealed partial class DebugNpcTool
    {
        private void OnClick(InputAction.CallbackContext _)
        {
            if (!_enabled) return;
            if (!_hasSelectedNpc) return;
            if (_world == null || _grid == null) return;

            if (!_world.Npcs.Exists(_selectedNpc))
            {
                _hasSelectedNpc = false;
                return;
            }

            if (!TryGetCellUnderMouse(out var cell))
                return;

            if (!_grid.IsInside(cell))
            {
                _noti?.Push("NpcAssign_OutOfBounds", "NPC", "Out of bounds.", NotificationSeverity.Warning, default, 0.2f, true);
                return;
            }

            var occ = _grid.Get(cell);
            if (occ.Kind != CellOccupancyKind.Building || occ.Building.Value == 0)
            {
                _noti?.Push("NpcAssign_NotBuilding", "NPC", $"Not a building cell ({cell.X},{cell.Y}).", NotificationSeverity.Warning, default, 0.2f, true);
                return;
            }

            var buildingId = occ.Building;
            var npc = _world.Npcs.Get(_selectedNpc);

            try
            {
                var bs = _world.Buildings.Get(buildingId);
                if (_data != null && _data.TryGetBuilding(bs.DefId, out var def) && def != null)
                {
                    int max = GetMaxAssignedFor(def, bs.Level);
                    if (max <= 0)
                    {
                        _noti?.Push($"NpcAssign_NoSlots_{_selectedNpc.Value}", "NPC",
                            $"Building {bs.DefId} không nhận worker.",
                            NotificationSeverity.Warning, default, 0.35f, true);
                        return;
                    }

                    int assigned = CountAssignedToBuilding(buildingId, excludeNpc: _selectedNpc);
                    if (assigned >= max)
                    {
                        _noti?.Push($"NpcAssign_Full_{_selectedNpc.Value}", "NPC",
                            $"Building {bs.DefId} đã đủ worker ({assigned}/{max}).",
                            NotificationSeverity.Warning, default, 0.6f, true);
                        return;
                    }
                }
            }
            catch
            {
                _noti?.Push($"NpcAssign_LimitCheckFail_{_selectedNpc.Value}", "NPC",
                    "Assign limit check failed (exception). Check Console.",
                    NotificationSeverity.Warning, default, 0.5f, true);
            }

            if (npc.CurrentJob.Value != 0)
            {
                ForceReleaseSelectedNpc("Assign", keepWorkplace: false);
                npc = _world.Npcs.Get(_selectedNpc);
            }

            npc.Workplace = buildingId;
            npc.IsIdle = true;
            npc.CurrentJob = default;
            _world.Npcs.Set(_selectedNpc, npc);
            _bus?.Publish(new NPCAssignedEvent(_selectedNpc, buildingId));

            _noti?.Push($"NpcAssigned_{_selectedNpc.Value}", "NPC",
                $"NPC #{_selectedNpc.Value} assigned to Building #{buildingId.Value}",
                NotificationSeverity.Info, default, 0.15f, true);

            try
            {
                var bs = _world.Buildings.Get(buildingId);
                if (_data == null || !_data.TryGetBuilding(bs.DefId, out var def) || def == null)
                    return;

                if (def.WorkRoles == WorkRoleFlags.None)
                {
                    _noti?.Push($"NpcAssigned_NoRoles_{_selectedNpc.Value}", "NPC",
                        $"Building {bs.DefId} has no WorkRoles => NPC will idle (expected).",
                        NotificationSeverity.Info, default, 0.3f, true);
                    return;
                }

                int q = (_jobs != null) ? _jobs.CountForWorkplace(buildingId) : -1;

                if ((def.WorkRoles & WorkRoleFlags.Harvest) != 0)
                {
                    var rt = GuessHarvestResourceType(bs.DefId);
                    if (rt != ResourceType.None)
                    {
                        var zc = _world.Zones.PickCell(rt, bs.Anchor);
                        if (zc.X == 0 && zc.Y == 0)
                        {
                            _noti?.Push($"NpcAssigned_NoZone_{_selectedNpc.Value}", "NPC",
                                $"No zone cell for {rt} near {bs.DefId} => Harvest cannot start. Check zones seeding.",
                                NotificationSeverity.Warning, default, 0.6f, true);
                            return;
                        }
                    }

                    if (_jobs != null && q == 0)
                    {
                        _jobs.Enqueue(new Job
                        {
                            Archetype = JobArchetype.Harvest,
                            Status = JobStatus.Created,
                            Workplace = buildingId
                        });

                        _noti?.Push($"NpcAssigned_EnqueueHarvest_{_selectedNpc.Value}", "NPC",
                            $"Enqueued 1 Harvest job for {bs.DefId} => NPC should start moving.",
                            NotificationSeverity.Info, default, 0.3f, true);
                    }
                    else if (q == 0)
                    {
                        _noti?.Push($"NpcAssigned_NoJobBoard_{_selectedNpc.Value}", "NPC",
                            $"JobBoard is null => cannot enqueue jobs; NPC will idle.",
                            NotificationSeverity.Warning, default, 0.6f, true);
                    }

                    return;
                }

                if (_jobs != null && q == 0)
                {
                    _noti?.Push($"NpcAssigned_NoJobs_{_selectedNpc.Value}", "NPC",
                        $"No jobs queued for {bs.DefId}. If this is HQ/Warehouse => needs producer local goods; if Build => needs Site; otherwise wait for scheduler.",
                        NotificationSeverity.Info, default, 0.5f, true);
                }
            }
            catch
            {
                _noti?.Push($"NpcAssigned_DiagFail_{_selectedNpc.Value}", "NPC",
                    "Assign diagnostics failed (exception). Check Console for details.",
                    NotificationSeverity.Warning, default, 0.5f, true);
            }
        }

        private void OnReleaseAllClaims(InputAction.CallbackContext _)
        {
            if (!_enabled) return;

            var kb = Keyboard.current;
            bool keepWorkplace = kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed);
            ForceReleaseSelectedNpc(keepWorkplace ? "Ctrl+R" : "R", keepWorkplace);
        }

        private void ForceReleaseSelectedNpc(string source, bool keepWorkplace)
        {
            if (!_hasSelectedNpc) return;
            if (_world == null || _claims == null) return;
            if (!_world.Npcs.Exists(_selectedNpc)) { _hasSelectedNpc = false; return; }

            var npc = _world.Npcs.Get(_selectedNpc);
            var curJobId = npc.CurrentJob;
            if (curJobId.Value != 0 && _jobs != null && _jobs.TryGet(curJobId, out var job))
            {
                job.Status = JobStatus.Cancelled;
                job.ClaimedBy = default;
                _jobs.Update(job);
            }

            _claims.ReleaseAll(_selectedNpc);
            npc.IsIdle = true;
            npc.CurrentJob = default;
            if (!keepWorkplace)
                npc.Workplace = default;
            _world.Npcs.Set(_selectedNpc, npc);

            _noti?.Push("ClaimsReleaseAll", "Claims",
                $"ForceRelease NPC #{_selectedNpc.Value} job={(curJobId.Value != 0 ? curJobId.Value : 0)} ({source})",
                NotificationSeverity.Info, default, 0.15f, true);
        }
    }
}
