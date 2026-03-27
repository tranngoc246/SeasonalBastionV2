using SeasonalBastion.Contracts;
using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Presenters
{
    public sealed class RunEndedModalPresenter : UiPresenterBase
    {
        private GameServices _s;
        private Label _title;
        private Label _body;
        private Button _btnRetry;
        private Button _btnMenu;

        protected override void OnBind()
        {
            _s = Ctx?.Services as GameServices;

            Root.AddToClassList(UiKeys.Class_BlockWorld);
            Root.pickingMode = PickingMode.Position;

            _title = Root.Q<Label>("LblRunEndedTitle");
            _body = Root.Q<Label>("LblRunEndedBody");
            _btnRetry = Root.Q<Button>("BtnRetry");
            _btnMenu = Root.Q<Button>("BtnRunEndedMenu");

            if (_btnRetry != null) _btnRetry.clicked += OnRetry;
            if (_btnMenu != null) _btnMenu.clicked += OnMenu;

            _s?.EventBus?.Subscribe<RunEndedEvent>(OnRunEnded);
        }

        protected override void OnUnbind()
        {
            if (_btnRetry != null) _btnRetry.clicked -= OnRetry;
            if (_btnMenu != null) _btnMenu.clicked -= OnMenu;

            _s?.EventBus?.Unsubscribe<RunEndedEvent>(OnRunEnded);
            _s = null;
        }

        protected override void OnRefresh()
        {
            if (_s?.RunOutcomeService == null)
            {
                SetText("RUN ENDED", "The run has ended.");
                return;
            }

            ApplyOutcome(_s.RunOutcomeService.Outcome, _s.RunOutcomeService.Reason);
        }

        private void OnRunEnded(RunEndedEvent e)
        {
            ApplyOutcome(e.Outcome, e.Reason);

            Ctx?.Modals?.CloseAll();
            Ctx?.Modals?.Push(UiKeys.Modal_RunEnded);
        }

        private void ApplyOutcome(RunOutcome outcome, RunEndReason reason)
        {
            switch (outcome)
            {
                case RunOutcome.Defeat:
                    SetText("DEFEAT", BuildDefeatBody(reason));
                    break;
                case RunOutcome.Victory:
                    SetText("VICTORY", BuildVictoryBody(reason));
                    break;
                case RunOutcome.Abort:
                    SetText("RUN ABORTED", "The run was aborted.");
                    break;
                default:
                    SetText("RUN ENDED", "The run has ended.");
                    break;
            }
        }

        private static string BuildDefeatBody(RunEndReason reason)
        {
            return reason switch
            {
                RunEndReason.HqDestroyed => "Your HQ has fallen.",
                _ => "Your settlement has been defeated."
            };
        }

        private static string BuildVictoryBody(RunEndReason reason)
        {
            return reason switch
            {
                RunEndReason.SurvivedWinterYear2 => "You survived through the end of Winter, Year 2.",
                RunEndReason.SurvivedWinterYear1 => "You survived through the end of Winter, Year 1.",
                RunEndReason.FinalWaveCleared => "You survived the final assault.",
                _ => "You survived the run."
            };
        }

        private void SetText(string title, string body)
        {
            if (_title != null) _title.text = title ?? "";
            if (_body != null) _body.text = body ?? "";
        }

        private void OnRetry()
        {
            if (GameAppController.Instance == null) return;
            GameAppController.Instance.RequestNewGame(seed: 0, wipeExistingSave: true);
        }

        private void OnMenu()
        {
            if (GameAppController.Instance == null) return;
            GameAppController.Instance.GoToMainMenu();
        }
    }
}
