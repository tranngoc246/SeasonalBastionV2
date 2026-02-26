using UnityEngine;

namespace SeasonalBastion
{
    [CreateAssetMenu(menuName = "SeasonalBastion/DefsCatalog")]
    public sealed class DefsCatalog : ScriptableObject
    {
        public TextAsset Buildings;
        public TextAsset Npcs;
        public TextAsset Towers;
        public TextAsset Enemies;
        public TextAsset Recipes;
        public TextAsset Waves;
        public TextAsset Rewards;
        public TextAsset Balance;
        public TextAsset BuildablesGraph;
    }
}
