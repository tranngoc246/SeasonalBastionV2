using System;
using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SeasonalBastion
{
    internal sealed class PlacementPreviewRenderer
    {
        private readonly Grid _grid;
        private readonly IGridMap _gridMap;
        private readonly IDataRegistry _data;
        private readonly IPlacementService _placement;
        private readonly Tilemap _previewTilemap;
        private readonly TileBase _tileOk;
        private readonly TileBase _tileBad;
        private readonly TileBase _tileDriveway;
        private readonly TileBase _tileEntryValid;
        private readonly TileBase _tileEntryInvalid;
        private readonly TileBase _tileFrontNorth;
        private readonly TileBase _tileFrontEast;
        private readonly TileBase _tileFrontSouth;
        private readonly TileBase _tileFrontWest;
        private readonly bool _useGhostSprite;
        private readonly Sprite _ghostSprite;
        private readonly float _ghostAlpha;
        private readonly float _ghostFill;
        private readonly string _ghostSortingLayer;
        private readonly bool _useFrontArrowSprite;
        private readonly Sprite _frontArrowSprite;
        private readonly float _frontArrowWidthCells;
        private readonly float _frontArrowLengthCells;
        private readonly string _frontArrowSortingLayer;
        private readonly Transform _parent;

        private readonly List<Vector3Int> _prevCells = new(64);
        private bool _hasPrevDriveway;
        private Vector3Int _prevDriveway;
        private bool _hasPrevFrontTile;
        private Vector3Int _prevFrontTile;
        private string _prevDef;
        private Dir4 _prevRot;
        private UiToolMode _prevTool;
        private CellPos _prevCell;
        private SpriteRenderer _ghostSr;
        private SpriteRenderer _frontArrowSr;

        public PlacementPreviewRenderer(
            Grid grid,
            IGridMap gridMap,
            IDataRegistry data,
            IPlacementService placement,
            Tilemap previewTilemap,
            TileBase tileOk,
            TileBase tileBad,
            TileBase tileDriveway,
            TileBase tileEntryValid,
            TileBase tileEntryInvalid,
            TileBase tileFrontNorth,
            TileBase tileFrontEast,
            TileBase tileFrontSouth,
            TileBase tileFrontWest,
            bool useGhostSprite,
            Sprite ghostSprite,
            float ghostAlpha,
            float ghostFill,
            string ghostSortingLayer,
            bool useFrontArrowSprite,
            Sprite frontArrowSprite,
            float frontArrowWidthCells,
            float frontArrowLengthCells,
            string frontArrowSortingLayer,
            Transform parent)
        {
            _grid = grid;
            _gridMap = gridMap;
            _data = data;
            _placement = placement;
            _previewTilemap = previewTilemap;
            _tileOk = tileOk;
            _tileBad = tileBad;
            _tileDriveway = tileDriveway;
            _tileEntryValid = tileEntryValid;
            _tileEntryInvalid = tileEntryInvalid;
            _tileFrontNorth = tileFrontNorth;
            _tileFrontEast = tileFrontEast;
            _tileFrontSouth = tileFrontSouth;
            _tileFrontWest = tileFrontWest;
            _useGhostSprite = useGhostSprite;
            _ghostSprite = ghostSprite;
            _ghostAlpha = ghostAlpha;
            _ghostFill = ghostFill;
            _ghostSortingLayer = ghostSortingLayer;
            _useFrontArrowSprite = useFrontArrowSprite;
            _frontArrowSprite = frontArrowSprite;
            _frontArrowWidthCells = frontArrowWidthCells;
            _frontArrowLengthCells = frontArrowLengthCells;
            _frontArrowSortingLayer = frontArrowSortingLayer;
            _parent = parent;
        }

        public void EnsureObjects()
        {
            EnsureGhost();
            EnsureFrontArrow();
        }

        public void HideAll()
        {
            ClearPreview();
            SetGhostVisible(false);
            SetFrontArrowVisible(false);
        }

        public PlacementResult? UpdatePreview(CellPos cell, string placeDefId, Dir4 rot, UiToolMode tool)
        {
            if (_prevCell.X == cell.X && _prevCell.Y == cell.Y
                && _prevTool == tool
                && _prevRot == rot
                && string.Equals(_prevDef, placeDefId, StringComparison.Ordinal))
            {
                return null;
            }

            _prevCell = cell;
            _prevTool = tool;
            _prevRot = rot;
            _prevDef = placeDefId;

            ClearPreview();
            if (_previewTilemap == null)
                return null;

            if (!string.IsNullOrEmpty(placeDefId))
                return RenderBuildingPreview(cell, placeDefId, rot);

            if (tool == UiToolMode.Road)
            {
                RenderSingleCellPreview(cell, _placement.CanPlaceRoad(cell));
                return null;
            }

            if (tool == UiToolMode.Remove)
            {
                RenderSingleCellPreview(cell, _placement.CanRemoveRoad(cell));
                return null;
            }

            SetGhostVisible(false);
            SetFrontArrowVisible(false);
            return null;
        }

        private PlacementResult RenderBuildingPreview(CellPos cell, string placeDefId, Dir4 rot)
        {
            int width = 1;
            int height = 1;
            if (_data != null && _data.TryGetBuilding(placeDefId, out var def) && def != null)
            {
                width = Mathf.Max(1, def.SizeX);
                height = Mathf.Max(1, def.SizeY);
            }

            var result = _placement.ValidateBuilding(placeDefId, cell, rot);
            var footprintTile = result.Ok ? _tileOk : _tileBad;

            for (int dy = 0; dy < height; dy++)
            {
                for (int dx = 0; dx < width; dx++)
                {
                    var previewCell = new CellPos(cell.X + dx, cell.Y + dy);
                    if (_gridMap != null && !_gridMap.IsInside(previewCell))
                        continue;

                    var tilePos = new Vector3Int(previewCell.X, previewCell.Y, 0);
                    if (footprintTile != null)
                    {
                        _previewTilemap.SetTile(tilePos, footprintTile);
                        _prevCells.Add(tilePos);
                    }
                }
            }

            if (_gridMap != null && _gridMap.IsInside(result.SuggestedRoadCell))
            {
                _prevDriveway = new Vector3Int(result.SuggestedRoadCell.X, result.SuggestedRoadCell.Y, 0);
                var drivewayTile = result.Ok
                    ? (_tileEntryValid != null ? _tileEntryValid : _tileDriveway)
                    : (_tileEntryInvalid != null ? _tileEntryInvalid : (_tileBad != null ? _tileBad : _tileDriveway));
                if (drivewayTile != null)
                {
                    _previewTilemap.SetTile(_prevDriveway, drivewayTile);
                    _hasPrevDriveway = true;
                }
            }

            UpdateFrontMarker(cell, width, height, rot, result.Ok);
            UpdateGhost(cell, width, height, result.Ok);
            return result;
        }

        private void RenderSingleCellPreview(CellPos cell, bool ok)
        {
            var tile = ok ? _tileOk : _tileBad;
            if (tile != null)
            {
                var tilePos = new Vector3Int(cell.X, cell.Y, 0);
                _previewTilemap.SetTile(tilePos, tile);
                _prevCells.Add(tilePos);
            }

            SetGhostVisible(false);
            SetFrontArrowVisible(false);
        }

        private void ClearPreview()
        {
            if (_previewTilemap != null)
            {
                for (int i = 0; i < _prevCells.Count; i++)
                    _previewTilemap.SetTile(_prevCells[i], null);
                _prevCells.Clear();

                if (_hasPrevDriveway)
                {
                    _previewTilemap.SetTile(_prevDriveway, null);
                    _hasPrevDriveway = false;
                }

                if (_hasPrevFrontTile)
                {
                    _previewTilemap.SetTile(_prevFrontTile, null);
                    _hasPrevFrontTile = false;
                }
            }

            SetFrontArrowVisible(false);
        }

        private void EnsureGhost()
        {
            if (!_useGhostSprite)
                return;

            var go = new GameObject("GhostBuilding");
            go.transform.SetParent(_parent, false);

            _ghostSr = go.AddComponent<SpriteRenderer>();
            _ghostSr.sortingLayerName = _ghostSortingLayer;
            _ghostSr.sortingOrder = 9999;
            _ghostSr.sprite = _ghostSprite;
            _ghostSr.enabled = false;
        }

        private void EnsureFrontArrow()
        {
            if (!_useFrontArrowSprite)
                return;

            var go = new GameObject("PlacementFrontMarker");
            go.transform.SetParent(_parent, false);

            _frontArrowSr = go.AddComponent<SpriteRenderer>();
            _frontArrowSr.sortingLayerName = _frontArrowSortingLayer;
            _frontArrowSr.sortingOrder = 10000;
            _frontArrowSr.sprite = _frontArrowSprite != null ? _frontArrowSprite : _ghostSprite;
            _frontArrowSr.enabled = false;
        }

        private void UpdateGhost(CellPos anchor, int sizeX, int sizeY, bool ok)
        {
            if (!_useGhostSprite || _ghostSr == null)
                return;

            if (_ghostSr.sprite == null)
                _ghostSr.sprite = _ghostSprite;
            if (_ghostSr.sprite == null)
            {
                _ghostSr.enabled = false;
                return;
            }

            Vector3 pos = FootprintCenterWorld(anchor, sizeX, sizeY);
            _ghostSr.transform.position = pos;
            _ghostSr.sortingOrder = -Mathf.RoundToInt(pos.y * 100f);
            _ghostSr.color = ok
                ? new Color(0.20f, 1.00f, 0.35f, Mathf.Max(_ghostAlpha, 0.42f))
                : new Color(1.00f, 0.16f, 0.16f, Mathf.Max(_ghostAlpha + 0.15f, 0.60f));
            ApplyScaleToFootprint(_ghostSr, sizeX, sizeY, ok ? _ghostFill : Mathf.Clamp01(_ghostFill - 0.08f));
            _ghostSr.enabled = true;
        }

        private void UpdateFrontMarker(CellPos anchor, int sizeX, int sizeY, Dir4 rot, bool ok)
        {
            if (TryDrawFrontMarkerTile(anchor, sizeX, sizeY, rot))
            {
                SetFrontArrowVisible(false);
                return;
            }

            if (!_useFrontArrowSprite || _frontArrowSr == null)
            {
                SetFrontArrowVisible(false);
                return;
            }

            if (_frontArrowSr.sprite == null)
                _frontArrowSr.sprite = _frontArrowSprite != null ? _frontArrowSprite : _ghostSprite;
            if (_frontArrowSr.sprite == null)
            {
                SetFrontArrowVisible(false);
                return;
            }

            Vector3 pos = GetFrontMarkerWorld(anchor, sizeX, sizeY, rot);
            _frontArrowSr.transform.position = pos;
            _frontArrowSr.sortingOrder = -Mathf.RoundToInt(pos.y * 100f) + 1;
            _frontArrowSr.transform.rotation = Quaternion.Euler(0f, 0f, GetRotationDegrees(rot));
            _frontArrowSr.color = ok
                ? new Color(1.00f, 0.90f, 0.15f, 0.95f)
                : new Color(1.00f, 0.35f, 0.15f, 1.00f);
            ApplyScaleToCellSize(_frontArrowSr, _frontArrowWidthCells, _frontArrowLengthCells);
            _frontArrowSr.enabled = true;
        }

        private void SetGhostVisible(bool visible)
        {
            if (_ghostSr == null)
                return;
            _ghostSr.enabled = visible;
        }

        private void SetFrontArrowVisible(bool visible)
        {
            if (_frontArrowSr == null)
                return;
            _frontArrowSr.enabled = visible;
        }

        private Vector3 FootprintCenterWorld(CellPos anchor, int sizeX, int sizeY)
        {
            Vector3 cellSize = _grid != null ? _grid.cellSize : Vector3.one;
            Vector3 anchorCenter = CellToWorldCenter(anchor);
            float offsetX = (sizeX * 0.5f - 0.5f) * cellSize.x;
            float offsetY = (sizeY * 0.5f - 0.5f) * cellSize.y;
            return anchorCenter + new Vector3(offsetX, offsetY, 0f);
        }

        private Vector3 GetFrontMarkerWorld(CellPos anchor, int sizeX, int sizeY, Dir4 rot)
        {
            Vector3 center = FootprintCenterWorld(anchor, sizeX, sizeY);
            Vector3 cellSize = _grid != null ? _grid.cellSize : Vector3.one;
            float halfWidth = Mathf.Max(0.5f, sizeX * 0.5f) * cellSize.x;
            float halfHeight = Mathf.Max(0.5f, sizeY * 0.5f) * cellSize.y;
            float insetX = cellSize.x * 0.32f;
            float insetY = cellSize.y * 0.32f;

            return rot switch
            {
                Dir4.N => center + new Vector3(0f, halfHeight - insetY, 0f),
                Dir4.S => center + new Vector3(0f, -halfHeight + insetY, 0f),
                Dir4.E => center + new Vector3(halfWidth - insetX, 0f, 0f),
                Dir4.W => center + new Vector3(-halfWidth + insetX, 0f, 0f),
                _ => center + new Vector3(0f, halfHeight - insetY, 0f),
            };
        }

        private Vector3 CellToWorldCenter(CellPos cell)
        {
            if (_grid != null)
                return _grid.GetCellCenterWorld(new Vector3Int(cell.X, cell.Y, 0));
            return new Vector3(cell.X + 0.5f, cell.Y + 0.5f, 0f);
        }

        private void ApplyScaleToFootprint(SpriteRenderer sr, int sizeX, int sizeY, float fill)
        {
            if (sr == null || sr.sprite == null)
                return;

            Vector3 cellSize = _grid != null ? _grid.cellSize : Vector3.one;
            float targetWidth = Mathf.Max(0.01f, sizeX * cellSize.x * fill);
            float targetHeight = Mathf.Max(0.01f, sizeY * cellSize.y * fill);
            Vector3 native = sr.sprite.bounds.size;
            float nativeWidth = Mathf.Max(0.0001f, native.x);
            float nativeHeight = Mathf.Max(0.0001f, native.y);
            sr.transform.localScale = new Vector3(targetWidth / nativeWidth, targetHeight / nativeHeight, 1f);
        }

        private void ApplyScaleToCellSize(SpriteRenderer sr, float widthCells, float heightCells)
        {
            if (sr == null || sr.sprite == null)
                return;

            Vector3 cellSize = _grid != null ? _grid.cellSize : Vector3.one;
            float targetWidth = Mathf.Max(0.01f, widthCells * cellSize.x);
            float targetHeight = Mathf.Max(0.01f, heightCells * cellSize.y);
            Vector3 native = sr.sprite.bounds.size;
            float nativeWidth = Mathf.Max(0.0001f, native.x);
            float nativeHeight = Mathf.Max(0.0001f, native.y);
            sr.transform.localScale = new Vector3(targetWidth / nativeWidth, targetHeight / nativeHeight, 1f);
        }

        private bool TryDrawFrontMarkerTile(CellPos anchor, int sizeX, int sizeY, Dir4 rot)
        {
            if (_previewTilemap == null)
                return false;

            var frontTile = GetFrontTile(rot);
            if (frontTile == null)
                return false;

            CellPos frontCell = GetFrontMarkerCell(anchor, sizeX, sizeY, rot);
            if (_gridMap != null && !_gridMap.IsInside(frontCell))
                return false;

            _prevFrontTile = new Vector3Int(frontCell.X, frontCell.Y, 0);
            _previewTilemap.SetTile(_prevFrontTile, frontTile);
            _hasPrevFrontTile = true;
            return true;
        }

        private TileBase GetFrontTile(Dir4 rot)
        {
            return rot switch
            {
                Dir4.N => _tileFrontNorth,
                Dir4.E => _tileFrontEast,
                Dir4.S => _tileFrontSouth,
                Dir4.W => _tileFrontWest,
                _ => _tileFrontNorth,
            };
        }

        private CellPos GetFrontMarkerCell(CellPos anchor, int sizeX, int sizeY, Dir4 rot)
        {
            int centerX = anchor.X + Mathf.Max(0, (sizeX - 1) / 2);
            int centerY = anchor.Y + Mathf.Max(0, (sizeY - 1) / 2);

            return rot switch
            {
                Dir4.N => new CellPos(centerX, anchor.Y + sizeY - 1),
                Dir4.S => new CellPos(centerX, anchor.Y),
                Dir4.E => new CellPos(anchor.X + sizeX - 1, centerY),
                Dir4.W => new CellPos(anchor.X, centerY),
                _ => new CellPos(centerX, anchor.Y + sizeY - 1),
            };
        }

        private static float GetRotationDegrees(Dir4 rot)
        {
            return rot switch
            {
                Dir4.N => 0f,
                Dir4.E => -90f,
                Dir4.S => 180f,
                Dir4.W => 90f,
                _ => 0f,
            };
        }
    }
}
