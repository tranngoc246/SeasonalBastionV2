using System;

namespace SeasonalBastion.Contracts
{
    /// <summary>
    /// Upgrade Graph (Node/Edge).
    /// NodeId trùng với DefId của BuildingDef/TowerDef trong pipeline hiện tại.
    /// Graph chỉ định upgrade relationship + cost/chunks; stats vẫn ở Buildings/Towers.
    /// </summary>
    [Serializable]
    public sealed class BuildableNodeDef
    {
        public string Id = "";
        public int Level = 1;
        public bool Placeable = true; // t2/t3 thường false để ẩn khỏi BuildPanel
    }

    [Serializable]
    public sealed class UpgradeEdgeDef
    {
        public string Id = "";
        public string From = "";
        public string To = "";

        // Cost tăng thêm khi upgrade (delta cost)
        public CostDef[] Cost;

        // Work chunks (convert ra seconds bằng Balance.build.workChunkSec + builder tier mult)
        public int WorkChunks = 0;

        // Optional: gating bằng UnlockService id (rỗng => không gate)
        public string RequiresUnlocked = "";
    }
}