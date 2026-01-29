using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class StorageService : IStorageService
    {
        private readonly IWorldState _w;
        private readonly IDataRegistry _data;
        private readonly IEventBus _bus;

        public StorageService(IWorldState w, IDataRegistry data, IEventBus bus)
        { _w = w; _data = data; _bus = bus; }

        public StorageSnapshot GetStorage(BuildingId building)
        {
            if (!_w.Buildings.Exists(building))
                return new StorageSnapshot(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

            var st = _w.Buildings.Get(building);

            int cw = GetCap(building, ResourceType.Wood);
            int cf = GetCap(building, ResourceType.Food);
            int cs = GetCap(building, ResourceType.Stone);
            int ci = GetCap(building, ResourceType.Iron);
            int ca = GetCap(building, ResourceType.Ammo);

            int w = cw > 0 ? st.Wood : 0;
            int f = cf > 0 ? st.Food : 0;
            int s = cs > 0 ? st.Stone : 0;
            int i = ci > 0 ? st.Iron : 0;
            int a = ca > 0 ? st.Ammo : 0;

            return new StorageSnapshot(w, f, s, i, a, cw, cf, cs, ci, ca);
        }

        public bool CanStore(BuildingId building, ResourceType type)
        {
            if (!_w.Buildings.Exists(building)) return false;

            var st = _w.Buildings.Get(building);
            var def = _data.GetBuilding(st.DefId);
            int level = NormalizeLevel(st.Level);

            // HARD RULE: HQ/Warehouse forbid ammo (v0.1)
            if (type == ResourceType.Ammo && (def.IsHQ || def.IsWarehouse))
                return false;

            int cap = GetCapFromDef(def, type, level);
            if (cap <= 0) return false;

            // Optional hardening: ammo only at Forge/Armory (prevents accidental ammo caps on other defs)
            if (type == ResourceType.Ammo && !(def.IsForge || def.IsArmory))
                return false;

            return true;
        }

        public int GetAmount(BuildingId building, ResourceType type)
        {
            if (!_w.Buildings.Exists(building)) return 0;

            var st = _w.Buildings.Get(building);
            return type switch
            {
                ResourceType.Wood => st.Wood,
                ResourceType.Food => st.Food,
                ResourceType.Stone => st.Stone,
                ResourceType.Iron => st.Iron,
                ResourceType.Ammo => st.Ammo,
                _ => 0
            };
        }

        public int GetCap(BuildingId building, ResourceType type)
        {
            if (!CanStore(building, type)) return 0;

            var st = _w.Buildings.Get(building);
            var def = _data.GetBuilding(st.DefId);
            int level = NormalizeLevel(st.Level);

            return GetCapFromDef(def, type, level);
        }

        public int Add(BuildingId building, ResourceType type, int amount)
        {
            if (amount <= 0) return 0;
            if (!_w.Buildings.Exists(building)) return 0;

            var st = _w.Buildings.Get(building);
            var def = _data.GetBuilding(st.DefId);

            if (!CanStore(building, type)) return 0;

            int cap = GetCap(building, type);
            if (cap <= 0) return 0;

            int cur = GetAmountFromState(st, type);
            int add = cap - cur;
            if (add <= 0) return 0;

            if (amount < add) add = amount;

            ApplyDelta(ref st, type, +add);
            _w.Buildings.Set(building, st);

            // publish delivered event only for HQ / Warehouse (Part27 Day9)
            if (add > 0 && (def.IsHQ || def.IsWarehouse))
                _bus.Publish(new ResourceDeliveredEvent(type, add, building));

            return add;
        }

        public int Remove(BuildingId building, ResourceType type, int amount)
        {
            if (amount <= 0) return 0;
            if (!_w.Buildings.Exists(building)) return 0;

            if (!CanStore(building, type)) return 0;

            var st = _w.Buildings.Get(building);
            int cur = GetAmountFromState(st, type);
            if (cur <= 0) return 0;

            int rem = amount < cur ? amount : cur;

            ApplyDelta(ref st, type, -rem);
            _w.Buildings.Set(building, st);
                        
            return rem;
        }

        public int GetTotal(ResourceType type)
        {
            int sum = 0;
            foreach (var id in _w.Buildings.Ids)
            {
                if (!CanStore(id, type)) continue;
                sum += GetAmount(id, type);
            }
            return sum;
        }

        // ---------------------------
        // helpers
        // ---------------------------

        private static int NormalizeLevel(int level) => level <= 0 ? 1 : (level > 3 ? 3 : level);

        private static int GetAmountFromState(in BuildingState st, ResourceType type)
        {
            return type switch
            {
                ResourceType.Wood => st.Wood,
                ResourceType.Food => st.Food,
                ResourceType.Stone => st.Stone,
                ResourceType.Iron => st.Iron,
                ResourceType.Ammo => st.Ammo,
                _ => 0
            };
        }

        private static void ApplyDelta(ref BuildingState st, ResourceType type, int delta)
        {
            switch (type)
            {
                case ResourceType.Wood: st.Wood += delta; break;
                case ResourceType.Food: st.Food += delta; break;
                case ResourceType.Stone: st.Stone += delta; break;
                case ResourceType.Iron: st.Iron += delta; break;
                case ResourceType.Ammo: st.Ammo += delta; break;
            }
        }

        private static int GetCapFromDef(BuildingDef def, ResourceType type, int level)
        {
            return type switch
            {
                ResourceType.Wood => def.CapWood.Get(level),
                ResourceType.Food => def.CapFood.Get(level),
                ResourceType.Stone => def.CapStone.Get(level),
                ResourceType.Iron => def.CapIron.Get(level),
                ResourceType.Ammo => def.CapAmmo.Get(level),
                _ => 0
            };
        }
    }
}
