namespace SeasonalBastion.Contracts
{
    public enum ClaimKind
    {
        StorageSource,
        StorageDest,
        TowerResupply,
        BuildSite,
        ProducerNode
    }

    public readonly struct ClaimKey
    {
        public readonly ClaimKind Kind;
        public readonly int A; // id value (building/tower/site)
        public readonly int B; // optional (resource type int)
        public ClaimKey(ClaimKind k,int a,int b){Kind=k;A=a;B=b;}
    }
}
