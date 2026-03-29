namespace SeasonalBastion.UI
{
    public enum SelectionKind
    {
        None = 0,
        Building = 1,
        ResourcePatch = 2
    }

    public readonly struct SelectionRef
    {
        public readonly SelectionKind Kind;
        public readonly int Id;

        public SelectionRef(SelectionKind kind, int id)
        {
            Kind = kind;
            Id = id;
        }

        public bool IsNone => Kind == SelectionKind.None || Id <= 0;

        public static SelectionRef None => new(SelectionKind.None, 0);
        public static SelectionRef Building(int id) => new(SelectionKind.Building, id);
        public static SelectionRef ResourcePatch(int id) => new(SelectionKind.ResourcePatch, id);
    }
}
