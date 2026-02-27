using UnityEngine.UIElements;

namespace SeasonalBastion.UI.Presenters
{
    public interface IUiPresenter
    {
        void Bind(UIContext ctx, VisualElement root);
        void Unbind();
        void Refresh();
    }
}