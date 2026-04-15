using System;
using UnityEngine.UIElements;

namespace KuonLib.AssetStash.Properties
{
    public abstract class StashProperty
    {
        protected Column column;
        protected Toggle toggle;

        bool isVisible;
        public bool IsVisible
        {
            get => isVisible;

            set
            {
                if (isVisible != value)
                {
                    isVisible = value;
                    if (toggle != null)
                    {
                        toggle.value = value;
                    }
                    if (column != null)
                    {
                        column.visible = value;
                    }

                    onChanged?.Invoke(value);
                }
            }
        }

        event Action<bool> onChanged;

        public event Action<bool> OnChanged
        {
            add => onChanged += value;
            remove => onChanged -= value;
        }

        public virtual void Create(Column column, Toggle toggle, bool isVisible) { }
        public virtual void BeginEdit(int id) { }
        public virtual void EndEdit(VisualElement e, AssetData item, string newText) { }
        public virtual void CancelEdit(VisualElement e) { }
    }
}
