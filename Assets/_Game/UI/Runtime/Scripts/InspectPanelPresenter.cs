using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.UIElements;
using System.Text;

namespace SeasonalBastion
{
    internal sealed class InspectPanelPresenter
    {
        private readonly GameServices _s;
        private readonly WorldSelectionController _sel;

        private readonly VisualElement _panel;
        private readonly VisualElement _panelsRoot;
        private readonly Button _btnClose;

        private readonly Label _lblId;
        private readonly Label _lblDef;
        private readonly Label _lblHp;

        private readonly Button _btnUpgrade;
        private readonly Label _lblUpgradeReq;

        private readonly Label _lblWood;
        private readonly Label _lblStone;
        private readonly Label _lblFood;
        private readonly Label _lblIron;
        private readonly Label _lblAmmo;

        private float _pollTimer;
        private readonly float _pollInterval;

        private BuildingId _lastId;
        private BuildingState _lastState;
        private bool _hasLast;

        public InspectPanelPresenter(VisualElement root, GameServices s, WorldSelectionController sel, float pollInterval = 0.33f)
        {
            _s = s;
            _sel = sel;
            _pollInterval = Mathf.Clamp(pollInterval, 0.15f, 1.0f);

            _panel = root.Q<VisualElement>("InspectPanel");
            _panelsRoot = root.Q<VisualElement>(className: "panels-root") ?? _panel?.parent;
            _btnClose = root.Q<Button>("BtnInspectClose");

            _lblId = root.Q<Label>("LblInspectId");
            _lblDef = root.Q<Label>("LblInspectDef");
            _lblHp = root.Q<Label>("LblInspectHP");
            _btnUpgrade = root.Q<Button>("BtnInspectUpgrade");
            _lblUpgradeReq = root.Q<Label>("LblInspectUpgradeReq");

            _lblWood = root.Q<Label>("LblInspectWood");
            _lblStone = root.Q<Label>("LblInspectStone");
            _lblFood = root.Q<Label>("LblInspectFood");
            _lblIron = root.Q<Label>("LblInspectIron");
            _lblAmmo = root.Q<Label>("LblInspectAmmo");
        }

        public void Bind()
        {
            if (_panelsRoot != null) _panelsRoot.pickingMode = PickingMode.Position;
            if (_panel != null) _panel.pickingMode = PickingMode.Position;         // panel receives clicks (default, but explicit is fine)

            if (_panel != null) _panel.AddToClassList("hidden");
            if (_btnClose != null) _btnClose.clicked += OnCloseClicked;
            if (_btnUpgrade != null) _btnUpgrade.clicked += OnUpgradeClicked;
        }

        public void Unbind()
        {
            if (_btnClose != null) _btnClose.clicked -= OnCloseClicked;
            if (_btnUpgrade != null) _btnUpgrade.clicked -= OnUpgradeClicked;
        }

        public void Tick(float dt)
        {
            if (_s == null || _s.WorldState == null || _s.WorldState.Buildings == null) return;
            if (_sel == null) return;

            _pollTimer += dt;
            if (_pollTimer < _pollInterval) return;
            _pollTimer = 0f;

            if (!_sel.HasSelection)
            {
                Hide();
                _hasLast = false;
                return;
            }

            var id = _sel.SelectedBuilding;
            if (id.Value == 0)
            {
                Hide();
                _hasLast = false;
                return;
            }

            if (!_s.WorldState.Buildings.TryGet(id, out var st))
            {
                // building bị destroy / không còn tồn tại
                Hide();
                _sel.ClearSelection();
                _hasLast = false;
                return;
            }

            Show();
            UpdateUpgradeUI(id, st);

            bool isTower = IsTowerBuilding(st);
            if (!_hasLast || id.Value != _lastId.Value || isTower || !IsSame(st, _lastState))
            {
                _lastId = id;
                _lastState = st;
                _hasLast = true;

                _lblId.text = $"ID: {st.Id.Value}";
                _lblDef.text = $"Def: {st.DefId ?? "-"}";

                if (st.MaxHP > 0)
                    _lblHp.text = $"HP: {st.HP}/{st.MaxHP}";
                else
                    _lblHp.text = "HP: -";

                _lblWood.text = $"Wood: {st.Wood}";
                _lblStone.text = $"Stone: {st.Stone}";
                _lblFood.text = $"Food: {st.Food}";
                _lblIron.text = $"Iron: {st.Iron}";
                if (TryGetTowerAmmoForBuilding(st, out int curAmmo, out int capAmmo))
                    _lblAmmo.text = $"Ammo: {curAmmo}/{capAmmo}";
                else
                    _lblAmmo.text = $"Ammo: {st.Ammo}";
            }
        }

        private void OnCloseClicked()
        {
            if (_sel != null) _sel.ClearSelection();
            Hide();
            _hasLast = false;
        }

        private void OnUpgradeClicked()
        {
            if (_s == null || _sel == null) return;
            if (_s.BuildOrderService == null) return;

            var bid = _sel.SelectedBuilding;
            if (bid.Value == 0) return;

            int orderId = _s.BuildOrderService.CreateUpgradeOrder(bid);

            _s.NotificationService?.Push(
                key: $"UpgradeClick_{bid.Value}",
                title: "Construction",
                body: orderId != 0 ? $"Upgrade order created (#{orderId})" : "Upgrade order failed",
                severity: orderId != 0 ? NotificationSeverity.Info : NotificationSeverity.Warning,
                payload: new NotificationPayload(bid, default, "upgrade"),
                cooldownSeconds: 0.15f,
                dedupeByKey: true
            );

            // Show immediate feedback in-panel (doesn't depend on toast system)
            if (_lblUpgradeReq != null)
            {
                _lblUpgradeReq.RemoveFromClassList("hidden");
                _lblUpgradeReq.text = orderId != 0
                    ? $"Đã tạo lệnh nâng cấp (Order #{orderId})."
                    : "Không thể tạo lệnh nâng cấp. Xem điều kiện / thiếu tài nguyên bên dưới.";
            }

            // Fallback toast (nếu bạn vẫn muốn)
            if (orderId == 0)
            {
                _s.NotificationService?.Push(
                    key: $"UpgradeFail_{bid.Value}",
                    title: "Construction",
                    body: "Upgrade failed (see conditions / resources / unlock).",
                    severity: NotificationSeverity.Warning,
                    payload: new NotificationPayload(bid, default, "upgrade"),
                    cooldownSeconds: 0.25f,
                    dedupeByKey: true
                );
            }
        }

        private void UpdateUpgradeUI(BuildingId bid, in BuildingState st)
        {
            if (_btnUpgrade == null) return;

            // Always show info area under the button
            if (_lblUpgradeReq != null)
            {
                _lblUpgradeReq.text = "";
                _lblUpgradeReq.RemoveFromClassList("hidden");
            }

            // 0) If this building is currently upgrading => show progress + disable button
            if (TryGetActiveUpgradeSite(bid, out var siteId, out var site))
            {
                _btnUpgrade.text = "Upgrading...";
                _btnUpgrade.SetEnabled(false);

                var sb = new StringBuilder(256);
                sb.Append("ĐANG NÂNG CẤP");
                sb.Append("\nTo: ").Append(string.IsNullOrEmpty(site.BuildingDefId) ? "-" : site.BuildingDefId);

                float pct = (site.WorkSecondsTotal > 0.0001f) ? (site.WorkSecondsDone / site.WorkSecondsTotal) : 0f;
                if (pct < 0f) pct = 0f; if (pct > 1f) pct = 1f;
                int pctI = (int)(pct * 100f + 0.5f);

                sb.Append("\nTiến độ: ").Append(pctI).Append("% (")
                  .Append(site.WorkSecondsDone.ToString("F1")).Append("/")
                  .Append(site.WorkSecondsTotal.ToString("F1")).Append("s)");

                if (site.RemainingCosts != null && site.RemainingCosts.Count > 0)
                {
                    sb.Append("\nCòn phải giao: ").Append(FormatCostList(site.RemainingCosts));

                    if (_s.StorageService != null)
                        sb.Append("\nTồn kho: ").Append(FormatStockForCosts(site.RemainingCosts));
                }
                else
                {
                    sb.Append("\nĐã giao đủ vật liệu. Đang thi công...");
                }

                SetUpgradeReqText(sb.ToString());
                return;
            }

            // Not upgrading
            _btnUpgrade.text = "Upgrade";
            _btnUpgrade.SetEnabled(false);

            if (_s == null || _s.DataRegistry == null)
            {
                SetUpgradeReqText("Không thể nâng cấp (DataRegistry chưa sẵn sàng).");
                return;
            }
            if (string.IsNullOrEmpty(st.DefId))
            {
                SetUpgradeReqText("Không thể nâng cấp (DefId rỗng).");
                return;
            }
            if (!st.IsConstructed)
            {
                SetUpgradeReqText("Chưa xây xong → hoàn thành xây dựng để nâng cấp.");
                return;
            }

            var edges = _s.DataRegistry.GetUpgradeEdgesFrom(st.DefId);
            if (edges == null || edges.Count == 0)
            {
                SetUpgradeReqText("Công trình này không có nhánh nâng cấp.");
                return;
            }

            var e = edges[0];

            bool unlocked = true;
            if (!string.IsNullOrWhiteSpace(e.RequiresUnlocked) && _s.UnlockService != null)
                unlocked = _s.UnlockService.IsUnlocked(e.RequiresUnlocked);

            _btnUpgrade.SetEnabled(unlocked);

            var sb2 = new StringBuilder(192);
            if (!unlocked) sb2.Append("LOCKED");
            sb2.Append("\nTo: ").Append(string.IsNullOrEmpty(e.To) ? "-" : e.To);

            var cost = FormatCost(e.Cost);
            if (cost.Length > 0) sb2.Append("\nCost: ").Append(cost);
            if (e.WorkChunks > 0) sb2.Append("\nWorkChunks: ").Append(e.WorkChunks);

            if (!string.IsNullOrWhiteSpace(e.RequiresUnlocked))
                sb2.Append("\nRequires unlock: ").Append(e.RequiresUnlocked);

            SetUpgradeReqText(sb2.ToString());
        }

        private bool TryGetActiveUpgradeSite(BuildingId bid, out SiteId sid, out BuildSiteState site)
        {
            sid = default;
            site = default;

            if (_s?.WorldState?.Sites == null) return false;

            foreach (var id in _s.WorldState.Sites.Ids)
            {
                if (!_s.WorldState.Sites.Exists(id)) continue;
                var st = _s.WorldState.Sites.Get(id);
                if (!st.IsActive) continue;
                if (!st.IsUpgrade) continue;
                if (st.TargetBuilding.Value != bid.Value) continue;

                sid = id;
                site = st;
                return true;
            }

            return false;
        }

        private void SetUpgradeReqText(string t)
        {
            if (_lblUpgradeReq == null) return;

            _lblUpgradeReq.text = t ?? "";
            if (string.IsNullOrEmpty(_lblUpgradeReq.text))
                _lblUpgradeReq.AddToClassList("hidden");
            else
                _lblUpgradeReq.RemoveFromClassList("hidden");
        }

        private static string FormatCostList(System.Collections.Generic.List<CostDef> list)
        {
            if (list == null || list.Count == 0) return "";
            var sb = new StringBuilder(64);
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (c == null || c.Amount <= 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(ResName(c.Resource)).Append(" ").Append(c.Amount);
            }
            return sb.ToString();
        }

        private string FormatStockForCosts(System.Collections.Generic.List<CostDef> list)
        {
            if (_s?.StorageService == null || list == null || list.Count == 0) return "";
            var sb = new StringBuilder(64);
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (c == null) continue;
                if (sb.Length > 0) sb.Append(", ");
                int total = _s.StorageService.GetTotal(c.Resource);
                sb.Append(ResName(c.Resource)).Append(" ").Append(total);
            }
            return sb.ToString();
        }

        private static string FormatCost(CostDef[] cost)
        {
            if (cost == null || cost.Length == 0) return "";
            var sb = new StringBuilder(64);
            for (int i = 0; i < cost.Length; i++)
            {
                var c = cost[i];
                if (c == null || c.Amount <= 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(ResName(c.Resource)).Append(" ").Append(c.Amount);
            }
            return sb.ToString();
        }

        private static string ResName(ResourceType t)
        {
            return t switch
            {
                ResourceType.Wood => "Wood",
                ResourceType.Food => "Food",
                ResourceType.Stone => "Stone",
                ResourceType.Iron => "Iron",
                ResourceType.Ammo => "Ammo",
                _ => t.ToString()
            };
        }

        private void Show()
        {
            if (_panel == null) return;
            _panel.RemoveFromClassList("hidden");
        }

        private void Hide()
        {
            if (_panel == null) return;
            _panel.AddToClassList("hidden");
        }

        private bool IsTowerBuilding(in BuildingState st)
        {
            if (_s == null || _s.DataRegistry == null) return false;
            if (string.IsNullOrEmpty(st.DefId)) return false;

            try
            {
                var def = _s.DataRegistry.GetBuilding(st.DefId);
                return def != null && def.IsTower;
            }
            catch { return false; }
        }

        private bool TryGetTowerAmmoForBuilding(in BuildingState st, out int ammo, out int cap)
        {
            ammo = 0; cap = 0;
            if (_s == null || _s.WorldState == null || _s.WorldState.Towers == null || _s.DataRegistry == null) return false;
            if (string.IsNullOrEmpty(st.DefId)) return false;

            BuildingDef bdef = null;
            try { bdef = _s.DataRegistry.GetBuilding(st.DefId); } catch { }
            if (bdef == null || !bdef.IsTower) return false;

            // TowerState.Cell là center cell
            var center = new CellPos(st.Anchor.X + (bdef.SizeX / 2), st.Anchor.Y + (bdef.SizeY / 2));

            foreach (var tid in _s.WorldState.Towers.Ids)
            {
                var ts = _s.WorldState.Towers.Get(tid);
                if (ts.Cell.X == center.X && ts.Cell.Y == center.Y)
                {
                    ammo = ts.Ammo;
                    cap = ts.AmmoCap;
                    return true;
                }
            }

            return false;
        }

        private static bool IsSame(in BuildingState a, in BuildingState b)
        {
            // compare only what we render (min set)
            return a.Id.Value == b.Id.Value
                && a.DefId == b.DefId
                && a.HP == b.HP
                && a.MaxHP == b.MaxHP
                && a.Wood == b.Wood
                && a.Stone == b.Stone
                && a.Food == b.Food
                && a.Iron == b.Iron
                && a.Ammo == b.Ammo;
        }
    }
}
