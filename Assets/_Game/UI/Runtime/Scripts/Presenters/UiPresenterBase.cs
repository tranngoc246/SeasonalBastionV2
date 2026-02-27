using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Presenters
{
    public abstract class UiPresenterBase : IUiPresenter
    {
        protected UIContext Ctx { get; private set; }
        protected VisualElement Root { get; private set; }
        protected bool Bound { get; private set; }

        public void Bind(UIContext ctx, VisualElement root)
        {
            if (Bound) return;

            Ctx = ctx;
            Root = root;

            if (Root == null) return;

            Bound = true;
            OnBind();
            Refresh();
        }

        public void Unbind()
        {
            if (!Bound) return;
            OnUnbind();
            Bound = false;
            Ctx = null;
            Root = null;
        }

        public void Refresh()
        {
            if (!Bound || Root == null) return;
            OnRefresh();
        }

        protected abstract void OnBind();
        protected abstract void OnUnbind();
        protected abstract void OnRefresh();
    }
}