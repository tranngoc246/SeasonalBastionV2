namespace SeasonalBastion.Contracts
{
    public interface IDataRegistry
    {
        T GetDef<T>(string id) where T : UnityEngine.Object;
        bool TryGetDef<T>(string id, out T def) where T : UnityEngine.Object;

        // common typed accessors:
        BuildingDef GetBuilding(string id);
        EnemyDef GetEnemy(string id);
        WaveDef GetWave(string id);
        RewardDef GetReward(string id);
        RecipeDef GetRecipe(string id);
    }
}
