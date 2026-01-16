// PATCH v0.1.1 — DefsCatalog compile + assembly separation
using UnityEngine;

namespace SeasonalBastion
{
    /// <summary>
    /// Authoring container for your definition sources (JSON/TextAsset/ScriptableObjects).
    /// Used by Boot/DataRegistry during startup.
    /// </summary>
    [CreateAssetMenu(menuName = "SeasonalBastion/DefsCatalog")]
    public sealed class DefsCatalog : ScriptableObject
    {
        // Minimal inputs for v0.1. You can swap these to ScriptableObject lists later.
        public TextAsset Buildings;
        public TextAsset Npcs;
        public TextAsset Towers;
        public TextAsset Enemies;
        public TextAsset Recipes;
        public TextAsset Waves;
        public TextAsset Rewards;
    }
}
