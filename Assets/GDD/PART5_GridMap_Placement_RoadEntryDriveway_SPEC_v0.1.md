# PART 5 — GRID / MAP / PLACEMENT + ROAD CONNECTIVITY + ENTRY/DRIVEWAY (SPEC) — v0.1

> Mục tiêu: chuẩn hoá toàn bộ rule “đặt công trình” để không phải làm lại về sau:
- Grid map data model (walkable/road/occupancy/reservation)
- Placement validation (footprint, bounds, collisions)
- **Entry + Road connectivity** theo chuẩn ổn định
- Driveway length (v0.1: 0..1) và rule chuyển driveway thành road
- API cho Tool/Build UI dùng (ghost preview, blocked reason)

Phần này KHÔNG làm JobSystem/Builder logic. Chỉ placement + grid state.

---

## 1) Core Concepts

### 1.1 Grid coordinate
- Logic chạy trên **cell coordinates** (int x,y)
- World<->Cell conversion qua GridLayout/Tilemap (presentation layer)

```csharp
public readonly struct CellPos
{
    public readonly int X;
    public readonly int Y;
    public CellPos(int x, int y) { X = x; Y = y; }

    public static CellPos operator +(CellPos a, CellPos b) => new(a.X+b.X, a.Y+b.Y);
    public override string ToString() => $"({X},{Y})";
}
```

### 1.2 Footprint anchor
- `Anchor` là cell “góc” (ví dụ bottom-left) của footprint WxH.
- Mọi tính toán footprint dùng anchor + width/height.

### 1.3 Entry point (v0.1)
- Entry được định nghĩa bởi `EntrySide` + `EntryOffset` (0..(sideLen-1))
- EntryCell là **cell nằm sát footprint** (ngoài footprint) tại cạnh đó.
- Sau này nếu cần entry world point “giữa cạnh” vẫn có thể derive từ cell + offset.

```csharp
public enum EntrySide { N, E, S, W }

public readonly struct EntryPoint
{
    public readonly EntrySide Side;
    public readonly int Offset; // along edge
    public EntryPoint(EntrySide side, int offset) { Side = side; Offset = offset; }

    public override string ToString() => $"{Side}[{Offset}]";
}
```

---

## 2) GridMap — State & Layers

### 2.1 Layers cần có
- `RoadLayer`: bool grid (road cells)
- `OccupancyLayer`: building occupancy (cell -> buildingId or none)
- `ReservationLayer`: agent reservation (cell -> agentId or none) (optional if you already have)

### 2.2 Data container
```csharp
public sealed class GridMap
{
    public readonly int Width;
    public readonly int Height;

    private readonly bool[] _road;          // size = W*H
    private readonly int[] _occupancy;      // 0=empty, else buildingId.Value
    private readonly int[] _reservation;    // 0=free, else agentId.Value

    public GridMap(int width, int height)
    {
        Width = width; Height = height;
        _road = new bool[width*height];
        _occupancy = new int[width*height];
        _reservation = new int[width*height];
    }

    public bool InBounds(CellPos p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

    public int Index(CellPos p) => p.Y * Width + p.X;

    // Road
    public bool IsRoad(CellPos p) => InBounds(p) && _road[Index(p)];
    public void SetRoad(CellPos p, bool isRoad) { if (InBounds(p)) _road[Index(p)] = isRoad; }

    // Occupancy
    public bool IsOccupied(CellPos p) => InBounds(p) && _occupancy[Index(p)] != 0;
    public int GetOccupant(CellPos p) => InBounds(p) ? _occupancy[Index(p)] : 0;
    public void SetOccupant(CellPos p, int buildingId) { if (InBounds(p)) _occupancy[Index(p)] = buildingId; }

    // Reservation
    public bool IsReserved(CellPos p) => InBounds(p) && _reservation[Index(p)] != 0;
    public int GetReservation(CellPos p) => InBounds(p) ? _reservation[Index(p)] : 0;
    public void SetReservation(CellPos p, int agentId) { if (InBounds(p)) _reservation[Index(p)] = agentId; }

    // Walkability for pathfinding (v0.1)
    public bool IsWalkable(CellPos p)
    {
        if (!InBounds(p)) return false;
        if (IsOccupied(p)) return false;
        return true;
    }
}
```

> NOTE: Nếu road tiles cũng “walkable”, thì `IsWalkable` không cần check road.  
> Pathfinding cost có thể ưu tiên road (road speed boost) — hệ đó nằm ngoài Part 5.

---

## 3) Footprint utilities

### 3.1 Iterate footprint cells
```csharp
public static class FootprintUtil
{
    public static void ForEachCell(CellPos anchor, int width, int height, System.Action<CellPos> fn)
    {
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                fn(new CellPos(anchor.X + x, anchor.Y + y));
    }

    public static bool Contains(CellPos anchor, int width, int height, CellPos p)
        => p.X >= anchor.X && p.Y >= anchor.Y
           && p.X < anchor.X + width && p.Y < anchor.Y + height;
}
```

---

## 4) Entry cell & driveway rules

### 4.1 Compute EntryCell from (anchor, footprint, entrypoint)
EntryCell nằm **ngoài footprint** đúng 1 cell, tại cạnh entry.

```csharp
public static class EntryUtil
{
    public static CellPos GetEntryCell(CellPos anchor, int w, int h, EntryPoint entry)
    {
        return entry.Side switch
        {
            EntrySide.N => new CellPos(anchor.X + Clamp(entry.Offset, 0, w-1), anchor.Y + h),
            EntrySide.S => new CellPos(anchor.X + Clamp(entry.Offset, 0, w-1), anchor.Y - 1),
            EntrySide.E => new CellPos(anchor.X + w, anchor.Y + Clamp(entry.Offset, 0, h-1)),
            EntrySide.W => new CellPos(anchor.X - 1, anchor.Y + Clamp(entry.Offset, 0, h-1)),
            _ => new CellPos(anchor.X, anchor.Y)
        };
    }

    private static int Clamp(int v, int min, int max)
        => v < min ? min : (v > max ? max : v);
}
```

### 4.2 Road connectivity definition (v0.1)
Nếu `BuildingDef.Footprint.RequiresRoadConnection == true`:
- Building được phép đặt nếu tồn tại road cell **trong khoảng cách <= DrivewayLength** tính từ EntryCell theo grid-orthogonal.
- `DrivewayLength` v0.1 chỉ 0 hoặc 1:
  - 0: EntryCell phải là road
  - 1: EntryCell hoặc 1 trong 4 neighbors của EntryCell là road

### 4.3 Driveway conversion rule
Nếu building requires road và thỏa điều kiện với `DrivewayLength==1`:
- Chọn **road cell gần EntryCell nhất**:
  - Nếu EntryCell là road -> driveway cell = EntryCell (no change)
  - Else pick one orthogonal neighbor của EntryCell là road (tie-break deterministic)
- Nếu driveway cell là neighbor (không phải EntryCell):
  - **EntryCell trở thành road** (driveway “được xây”)
  - (Optional) Nếu bạn muốn driveway là 1 cell road: chỉ cần convert EntryCell.
- Nếu `DrivewayLength==0`: không convert gì.

> Tie-break deterministic (để tránh save mismatch):
> order neighbor check: N, E, S, W (hoặc fixed).

---

## 5) Placement validation — Result model

### 5.1 Block reasons (để UI/notifications dùng)
```csharp
public enum PlacementFailReason
{
    None = 0,
    OutOfBounds,
    OverlapsBuilding,
    OverlapsReserved,
    EntryOutOfBounds,
    NoRoadConnection,
}
```

### 5.2 Result struct
```csharp
public readonly struct PlacementResult
{
    public readonly bool Ok;
    public readonly PlacementFailReason Reason;
    public readonly CellPos? ProblemCell; // optional
    public readonly CellPos EntryCell;

    public PlacementResult(bool ok, PlacementFailReason reason, CellPos? problemCell, CellPos entryCell)
    {
        Ok = ok; Reason = reason; ProblemCell = problemCell; EntryCell = entryCell;
    }

    public static PlacementResult Success(CellPos entryCell) => new(true, PlacementFailReason.None, null, entryCell);
    public static PlacementResult Fail(PlacementFailReason r, CellPos? cell, CellPos entryCell) => new(false, r, cell, entryCell);
}
```

---

## 6) BuildingPlacementService — API & logic

### 6.1 API
```csharp
public sealed class BuildingPlacementService
{
    private readonly GridMap _grid;

    public BuildingPlacementService(GridMap grid) { _grid = grid; }

    public PlacementResult Validate(BuildingDef def, CellPos anchor, EntryPoint entry)
    {
        var w = def.Footprint.Width;
        var h = def.Footprint.Height;

        // 1) bounds + occupancy
        CellPos? firstProblem = null;
        var ok = true;

        FootprintUtil.ForEachCell(anchor, w, h, cell =>
        {
            if (!ok) return;

            if (!_grid.InBounds(cell)) { ok = false; firstProblem = cell; return; }
            if (_grid.IsOccupied(cell)) { ok = false; firstProblem = cell; return; }
            if (_grid.IsReserved(cell)) { ok = false; firstProblem = cell; return; }
        });

        var entryCell = EntryUtil.GetEntryCell(anchor, w, h, entry);

        if (!ok)
        {
            var reason = !_grid.InBounds(firstProblem!.Value) ? PlacementFailReason.OutOfBounds
                        : _grid.IsOccupied(firstProblem!.Value) ? PlacementFailReason.OverlapsBuilding
                        : PlacementFailReason.OverlapsReserved;

            return PlacementResult.Fail(reason, firstProblem, entryCell);
        }

        // 2) entry bounds
        if (!_grid.InBounds(entryCell))
            return PlacementResult.Fail(PlacementFailReason.EntryOutOfBounds, entryCell, entryCell);

        // 3) road rule
        if (def.Footprint.RequiresRoadConnection)
        {
            var len = def.Footprint.DrivewayLength;
            if (!HasRoadWithin(entryCell, len))
                return PlacementResult.Fail(PlacementFailReason.NoRoadConnection, entryCell, entryCell);
        }

        return PlacementResult.Success(entryCell);
    }

    public DrivewayPlan ComputeDrivewayPlan(BuildingDef def, CellPos anchor, EntryPoint entry)
    {
        var w = def.Footprint.Width;
        var h = def.Footprint.Height;
        var entryCell = EntryUtil.GetEntryCell(anchor, w, h, entry);

        if (!def.Footprint.RequiresRoadConnection)
            return DrivewayPlan.None(entryCell);

        var len = def.Footprint.DrivewayLength;
        return DrivewayPlanner.Plan(_grid, entryCell, len);
    }

    private bool HasRoadWithin(CellPos entryCell, int len)
    {
        if (len <= 0) return _grid.IsRoad(entryCell);

        if (_grid.IsRoad(entryCell)) return true;

        // len==1
        foreach (var n in Neigh4(entryCell))
            if (_grid.IsRoad(n)) return true;

        return false;
    }

    private static System.Collections.Generic.IEnumerable<CellPos> Neigh4(CellPos p)
    {
        yield return new CellPos(p.X, p.Y + 1); // N
        yield return new CellPos(p.X + 1, p.Y); // E
        yield return new CellPos(p.X, p.Y - 1); // S
        yield return new CellPos(p.X - 1, p.Y); // W
    }
}
```

---

## 7) Driveway planning & application

### 7.1 Plan model
```csharp
public readonly struct DrivewayPlan
{
    public readonly bool NeedsConvertEntryToRoad;
    public readonly CellPos EntryCell;
    public readonly CellPos ConnectedRoadCell; // the road cell used to connect

    private DrivewayPlan(bool convert, CellPos entry, CellPos connectedRoad)
    {
        NeedsConvertEntryToRoad = convert;
        EntryCell = entry;
        ConnectedRoadCell = connectedRoad;
    }

    public static DrivewayPlan None(CellPos entry) => new(false, entry, entry);
    public static DrivewayPlan Convert(CellPos entry, CellPos connectedRoad) => new(true, entry, connectedRoad);
}
```

### 7.2 Planner
```csharp
public static class DrivewayPlanner
{
    public static DrivewayPlan Plan(GridMap grid, CellPos entryCell, int len)
    {
        if (len <= 0)
        {
            return DrivewayPlan.None(entryCell);
        }

        // len == 1
        if (grid.IsRoad(entryCell))
            return DrivewayPlan.None(entryCell);

        // deterministic neighbor order: N, E, S, W
        var n = new CellPos(entryCell.X, entryCell.Y + 1);
        if (grid.InBounds(n) && grid.IsRoad(n)) return DrivewayPlan.Convert(entryCell, n);

        var e = new CellPos(entryCell.X + 1, entryCell.Y);
        if (grid.InBounds(e) && grid.IsRoad(e)) return DrivewayPlan.Convert(entryCell, e);

        var s = new CellPos(entryCell.X, entryCell.Y - 1);
        if (grid.InBounds(s) && grid.IsRoad(s)) return DrivewayPlan.Convert(entryCell, s);

        var w = new CellPos(entryCell.X - 1, entryCell.Y);
        if (grid.InBounds(w) && grid.IsRoad(w)) return DrivewayPlan.Convert(entryCell, w);

        // should not happen if Validate passed
        return DrivewayPlan.Convert(entryCell, entryCell);
    }
}
```

### 7.3 Apply plan on placement success
- Khi building được đặt thành công (commit), hệ placement sẽ:
  1) Occupy footprint cells
  2) Nếu `plan.NeedsConvertEntryToRoad` => set road at EntryCell = true
- NOTE: Việc “xây driveway” có thể là instant ở v0.1 hoặc sau này thành job builder.

```csharp
public void CommitPlaceBuilding(int buildingId, BuildingDef def, CellPos anchor, EntryPoint entry)
{
    // occupancy
    FootprintUtil.ForEachCell(anchor, def.Footprint.Width, def.Footprint.Height, cell =>
        _grid.SetOccupant(cell, buildingId));

    // driveway
    var plan = ComputeDrivewayPlan(def, anchor, entry);
    if (plan.NeedsConvertEntryToRoad)
        _grid.SetRoad(plan.EntryCell, true);
}
```

---

## 8) Ghost Preview & UI feedback mapping

### 8.1 PlacementFailReason -> UI indicator
- OutOfBounds: highlight footprint red
- OverlapsBuilding: highlight overlapping cells
- OverlapsReserved: show “ô đang bị chiếm”
- EntryOutOfBounds: highlight entry cell
- NoRoadConnection: highlight entry + nearest road (if any) and show hint “cần nối road”

### 8.2 Notifications (optional hook)
- On confirm fail:
  - `build.blocked.no_road`
  - `path.reservation_fail` (nếu overlaps reserved)

---

## 9) Road placement rules (v0.1)
- Road placement only grid-orthogonal; each road cell sets `grid.SetRoad(cell,true)`
- Road cannot overlap occupied building cell:
  - Either block or allow (if you allow road under building, you must refactor pathing)
  - v0.1 khuyến nghị: **block road under building**

### 9.1 `RoadPlacementService`
```csharp
public sealed class RoadPlacementService
{
    private readonly GridMap _grid;
    public RoadPlacementService(GridMap grid) { _grid = grid; }

    public bool CanPlaceRoad(CellPos p)
        => _grid.InBounds(p) && !_grid.IsOccupied(p);

    public bool PlaceRoad(CellPos p)
    {
        if (!CanPlaceRoad(p)) return false;
        _grid.SetRoad(p, true);
        return true;
    }
}
```

---

## 10) Move building (future-proof hook)
Nếu sau này có “move building”:
- Validate placement tại vị trí mới
- Temporarily ignore occupancy của chính building khi validate:
  - pass in `ignoreBuildingId`
- Commit:
  - clear old occupancy cells
  - set new occupancy cells
  - apply driveway plan

(Chưa làm ở v0.1, nhưng API validate nên chừa slot)

```csharp
public PlacementResult Validate(BuildingDef def, CellPos anchor, EntryPoint entry, int ignoreBuildingId = 0)
```

---

## 11) Tests (EditMode)
- `EntryCell_Computed_Correctly_AllSides()`
- `Validate_Fails_OutOfBounds()`
- `Validate_Fails_OverlapOccupied()`
- `Validate_Fails_NoRoad_WhenRequired()`
- `Validate_Passes_WithDrivewayLen1_NeighborRoad()`
- `DrivewayPlan_Converts_EntryCell_WhenNeighborRoad()`
- `Commit_Sets_Occupancy_And_Converts_Road()`

---

## 12) Done Checklist (Part 5)
- [ ] GridMap có layers road/occupancy/reservation
- [ ] Placement validate footprint + entry + road rule
- [ ] Driveway plan deterministic (N/E/S/W tie-break)
- [ ] Commit đặt building set occupancy + driveway cell road
- [ ] Road placement orthogonal, blocked under buildings
- [ ] UI nhận PlacementFailReason để hiển thị blocked reason

---

## 13) Next Part (Part 6 đề xuất)
**Entity Stores + Building/NPC Runtime State** (data/state split) — nền cho JobSystem và economy.

