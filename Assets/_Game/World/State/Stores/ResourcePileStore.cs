using System.Collections.Generic;
using SeasonalBastion.Contracts;

namespace SeasonalBastion
{
    public sealed class ResourcePileStore : EntityStore<PileId, ResourcePileState>, IResourcePileStore
    {
        public override int ToInt(PileId id) => id.Value;
        public override PileId FromInt(int v) => new PileId(v);

        // Index đơn giản: key = cell + type + owner => pileId
        // (M4 scale nhỏ, dùng Dictionary OK)
        private readonly Dictionary<int, PileId> _byKey = new(256);

        public override void ClearAll()
        {
            base.ClearAll();
            _byKey.Clear();
        }

        public PileId AddOrIncrease(CellPos cell, ResourceType rt, int delta, BuildingId owner)
        {
            int key = MakeKey(cell, rt, owner);

            if (_byKey.TryGetValue(key, out var id) && Exists(id))
            {
                var st = Get(id);
                st.Amount += delta;
                if (st.Amount < 0) st.Amount = 0;
                Set(id, st);
                return id;
            }

            var ns = new ResourcePileState
            {
                Cell = cell,
                Resource = rt,
                Amount = delta < 0 ? 0 : delta,
                OwnerBuilding = owner
            };

            var newId = Create(ns);
            ns.Id = newId;
            Set(newId, ns);

            _byKey[key] = newId;
            return newId;
        }

        public bool TryTake(PileId id, int want, out int taken)
        {
            taken = 0;
            if (!Exists(id)) return false;

            var st = Get(id);
            if (st.Amount <= 0) return false;

            taken = want <= st.Amount ? want : st.Amount;
            st.Amount -= taken;

            if (st.Amount <= 0)
            {
                // remove entity + key mapping
                RemoveInternal(id);
                _byKey.Remove(MakeKey(st.Cell, st.Resource, st.OwnerBuilding));
            }
            else
            {
                Set(id, st);
            }

            return true;
        }

        public bool TryFindNonEmpty(ResourceType rt, BuildingId owner, out PileId id)
        {
            // scan all piles (M4 nhỏ, ok)
            foreach (var pid in Ids)
            {
                var st = Get(pid);
                if (st.Resource != rt) continue;
                if (st.OwnerBuilding.Value != owner.Value) continue;
                if (st.Amount > 0) { id = pid; return true; }
            }

            id = default;
            return false;
        }

        private static int MakeKey(CellPos c, ResourceType rt, BuildingId owner)
        {
            // grid 64x64 => pack safe
            // key = (x + y*128) + rt*16384 + owner*262144
            int xy = c.X + (c.Y << 7);
            return xy + ((int)rt << 14) + (owner.Value << 18);
        }

        // EntityStore không có Remove public trong bản của bạn -> dùng helper private
        private void RemoveInternal(PileId id)
        {
            // Trick: set default then call base.Remove if có, nếu không thì emulate:
            // Trong codebase của bạn EntityStore có usually method Destroy/Remove.
            // Nếu EntityStore của bạn đã có Remove(id) thì thay dòng dưới bằng Remove(id).
            base. Destroy(id);
        }

        public void Set(PileId id, in ResourcePileState st)
        {
            throw new System.NotImplementedException();
        }
    }
}
