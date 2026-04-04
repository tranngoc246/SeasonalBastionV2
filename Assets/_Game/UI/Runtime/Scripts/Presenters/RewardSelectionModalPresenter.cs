using SeasonalBastion.Contracts;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Presenters
{
    public sealed class RewardSelectionModalPresenter : UiPresenterBase
    {
        private GameServices _s;

        private Label _title;
        private Label _body;
        private Button _btnChoiceA;
        private Button _btnChoiceB;
        private Button _btnChoiceC;

        protected override void OnBind()
        {
            _s = Ctx?.Services as GameServices;

            Root.AddToClassList(UiKeys.Class_BlockWorld);
            Root.pickingMode = PickingMode.Position;

            _title = Root.Q<Label>("LblRewardTitle");
            _body = Root.Q<Label>("LblRewardBody");
            _btnChoiceA = Root.Q<Button>("BtnRewardA");
            _btnChoiceB = Root.Q<Button>("BtnRewardB");
            _btnChoiceC = Root.Q<Button>("BtnRewardC");

            if (_btnChoiceA != null) _btnChoiceA.clicked += OnChooseA;
            if (_btnChoiceB != null) _btnChoiceB.clicked += OnChooseB;
            if (_btnChoiceC != null) _btnChoiceC.clicked += OnChooseC;

            if (_s?.RewardService != null)
            {
                _s.RewardService.OnSelectionStarted += OnSelectionStarted;
                _s.RewardService.OnSelectionEnded += OnSelectionEnded;
            }
        }

        protected override void OnUnbind()
        {
            if (_btnChoiceA != null) _btnChoiceA.clicked -= OnChooseA;
            if (_btnChoiceB != null) _btnChoiceB.clicked -= OnChooseB;
            if (_btnChoiceC != null) _btnChoiceC.clicked -= OnChooseC;

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

            ApplyButton(_btnChoiceA, offer.A);
            ApplyButton(_btnChoiceB, offer.B);
            ApplyButton(_btnChoiceC, offer.C);
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

        private void OnChooseA() => _s?.RewardService?.Choose(0);
        private void OnChooseB() => _s?.RewardService?.Choose(1);
        private void OnChooseC() => _s?.RewardService?.Choose(2);

        private static void ApplyButton(Button button, string rewardId)
        {
            if (button == null) return;
            button.text = $"{GetRewardTitle(rewardId)}\n{GetRewardDescription(rewardId)}";
            button.SetEnabled(!string.IsNullOrWhiteSpace(rewardId));
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
    }
}
