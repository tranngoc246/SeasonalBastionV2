using System.Collections.Generic;
using SeasonalBastion.Contracts;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Presenters
{
    public sealed class RewardSelectionModalPresenter : UiPresenterBase
    {
        private GameServices _s;

        private Label _title;
        private Label _body;
        private ListView _rewardList;
        private readonly List<RewardOptionViewModel> _rewardItems = new(3);

        protected override void OnBind()
        {
            _s = Ctx?.Services as GameServices;

            Root.AddToClassList(UiKeys.Class_BlockWorld);
            Root.pickingMode = PickingMode.Position;

            _title = Root.Q<Label>("LblRewardTitle");
            _body = Root.Q<Label>("LblRewardBody");
            _rewardList = Root.Q<ListView>("RewardList");

            ConfigureRewardList();

            if (_s?.RewardService != null)
            {
                _s.RewardService.OnSelectionStarted += OnSelectionStarted;
                _s.RewardService.OnSelectionEnded += OnSelectionEnded;
            }
        }

        protected override void OnUnbind()
        {
            _rewardItems.Clear();

            if (_s?.RewardService != null)
            {
                _s.RewardService.OnSelectionStarted -= OnSelectionStarted;
                _s.RewardService.OnSelectionEnded -= OnSelectionEnded;
            }

            _s = null;
        }

        protected override void OnRefresh()
        {
            if (_title != null) _title.text = "CHOOSE A REWARD";
            if (_body != null) _body.text = "Pick 1 of 3. Effect applies immediately.";

            var offer = _s != null && _s.RewardService != null ? _s.RewardService.CurrentOffer : default;

            _rewardItems.Clear();
            AddRewardOption(offer.A, 0);
            AddRewardOption(offer.B, 1);
            AddRewardOption(offer.C, 2);

            if (_rewardList != null)
            {
                _rewardList.itemsSource = _rewardItems;
                _rewardList.Rebuild();
            }
        }

        private void OnSelectionStarted()
        {
            Refresh();
            Ctx?.Modals?.CloseAll();
            Ctx?.Modals?.Push(UiKeys.Modal_RewardSelection);
        }

        private void OnSelectionEnded()
        {
            Ctx?.Modals?.Pop();
        }

        private void ConfigureRewardList()
        {
            if (_rewardList == null)
                return;

            _rewardList.selectionType = SelectionType.None;
            _rewardList.makeItem = MakeRewardItem;
            _rewardList.bindItem = BindRewardItem;
            _rewardList.unbindItem = UnbindRewardItem;
            _rewardList.fixedItemHeight = 74f;
            _rewardList.itemsSource = _rewardItems;
        }

        private VisualElement MakeRewardItem()
        {
            var button = new Button();
            button.AddToClassList("reward-choice");
            button.RegisterCallback<ClickEvent>(OnRewardClicked);
            button.userData = new RewardButtonBinding(button);
            return button;
        }

        private void BindRewardItem(VisualElement element, int index)
        {
            if (element?.userData is not RewardButtonBinding binding)
                return;

            if (index < 0 || index >= _rewardItems.Count)
            {
                UnbindRewardItem(element, index);
                return;
            }

            var item = _rewardItems[index];
            if (item == null)
            {
                UnbindRewardItem(element, index);
                return;
            }

            binding.Index = item.Index;
            binding.Button.text = $"{GetRewardTitle(item.RewardId)}\n{GetRewardDescription(item.RewardId)}";
            binding.Button.SetEnabled(!string.IsNullOrWhiteSpace(item.RewardId));
        }

        private void UnbindRewardItem(VisualElement element, int index)
        {
            if (element?.userData is not RewardButtonBinding binding)
                return;

            binding.Index = -1;
            binding.Button.text = string.Empty;
            binding.Button.SetEnabled(false);
        }

        private void OnRewardClicked(ClickEvent evt)
        {
            if (evt?.currentTarget is not Button button || button.userData is not RewardButtonBinding binding)
                return;

            if (binding.Index >= 0)
                _s?.RewardService?.Choose(binding.Index);
        }

        private void AddRewardOption(string rewardId, int index)
        {
            _rewardItems.Add(new RewardOptionViewModel { RewardId = rewardId, Index = index });
        }

        private static string GetRewardTitle(string rewardId)
        {
            return rewardId switch
            {
                "Reward_BuildSpeed" => "+Build speed",
                "Reward_AmmoCapacity" => "+Ammo capacity",
                "Reward_TowerReload" => "+Tower reload speed",
                "Reward_NpcMoveSpeed" => "+NPC move speed",
                _ => rewardId ?? "Unknown reward",
            };
        }

        private static string GetRewardDescription(string rewardId)
        {
            return rewardId switch
            {
                "Reward_BuildSpeed" => "Builders work 15% faster this run.",
                "Reward_AmmoCapacity" => "All towers gain +5 ammo capacity.",
                "Reward_TowerReload" => "All towers reload/fire 12% faster.",
                "Reward_NpcMoveSpeed" => "NPCs move 10% faster this run.",
                _ => "Applies immediately.",
            };
        }

        private sealed class RewardOptionViewModel
        {
            public string RewardId { get; set; }
            public int Index { get; set; }
        }

        private sealed class RewardButtonBinding
        {
            public RewardButtonBinding(Button button)
            {
                Button = button;
            }

            public Button Button { get; }
            public int Index { get; set; } = -1;
        }
    }
}
