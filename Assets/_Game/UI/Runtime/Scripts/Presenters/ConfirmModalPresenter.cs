using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Presenters
{
    public sealed class ConfirmModalPresenter : UiPresenterBase
    {
        private Button _btnYes;
        private Button _btnNo;

        protected override void OnBind()
        {
            Root.AddToClassList(UiKeys.Class_BlockWorld);
            Root.pickingMode = PickingMode.Position;

            _btnYes = Root.Q<Button>("BtnYes");
            _btnNo = Root.Q<Button>("BtnNo");

            if (_btnYes != null) _btnYes.clicked += OnYes;
            if (_btnNo != null) _btnNo.clicked += OnNo;
        }

        protected override void OnUnbind()
        {
            if (_btnYes != null) _btnYes.clicked -= OnYes;
            if (_btnNo != null) _btnNo.clicked -= OnNo;
        }

        protected override void OnRefresh() { }

        private void OnYes()
        {
            // Close confirm
            Ctx?.Modals?.Pop();
        }

        private void OnNo()
        {
            Ctx?.Modals?.Pop();
        }
    }
}
