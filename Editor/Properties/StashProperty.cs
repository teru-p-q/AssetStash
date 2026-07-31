using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuonLib.AssetStash.Properties
{
    public abstract class StashProperty
    {
        protected readonly AssetStashTree stashTree;
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

        protected StashProperty(AssetStashTree tree)
        {
            stashTree = tree;
        }

        // Column/Toggle の可視性連携は全プロパティ共通のため基底でまとめて行う
        protected void SetupColumn(Column column, Toggle toggle, bool isVisible)
        {
            this.column = column;
            this.toggle = toggle;
            IsVisible = isVisible;

            if (toggle != null)
            {
                toggle.RegisterValueChangedCallback(evt => IsVisible = evt.newValue);
                AssetStashUtil.SetDefaultToggleStyle(toggle);
            }
        }

        // インラインテキスト編集の開始/終了処理は NameProperty / MemoProperty で共通のため基底に集約
        protected void BeginInlineEdit(ref int editId, int id)
        {
            editId = id;
            stashTree.Rebuild();
            stashTree.MarkDirtyRepaint();
        }

        protected void FinishInlineEdit(VisualElement editingElement, Action<VisualElement> restoreLabel)
        {
            try
            {
                var tf = stashTree.FindTextField(editingElement);
                var parent = tf.parent;
                if (parent != null)
                {
                    restoreLabel?.Invoke(parent);
                }
                tf.RemoveFromHierarchy();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            stashTree.MarkDirtyRepaint();
            stashTree.Focus();
        }

        protected static void RegisterInlineEditKeys(TextField tf, Action commit, Action cancel)
        {
            tf.RegisterCallback<KeyDownEvent>(ke =>
            {
                if (ke.keyCode == KeyCode.Return || ke.keyCode == KeyCode.KeypadEnter)
                {
                    commit();
                    ke.StopImmediatePropagation();
                }
                else if (ke.keyCode == KeyCode.Escape)
                {
                    cancel();
                    ke.StopImmediatePropagation();
                }
            });

            tf.RegisterCallback<FocusOutEvent>(fe => commit());
        }

        public virtual void Create(Column column, Toggle toggle, bool isVisible) { }
        public virtual void BeginEdit(int id) { }
        public virtual void EndEdit(VisualElement e, AssetData item, string newText) { }
        public virtual void CancelEdit(VisualElement e) { }
    }
}
