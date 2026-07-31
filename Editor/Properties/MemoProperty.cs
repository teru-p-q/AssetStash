using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuonLib.AssetStash.Properties
{
    public class MemoProperty : StashProperty
    {
        int editId = -1;

        public MemoProperty(AssetStashTree tree) : base(tree) { }

        Label FindLabelField(VisualElement ve) => ve.Query<Label>("Memo");

        public override void Create(Column column, Toggle toggle, bool isVisible)
        {
            SetupColumn(column, toggle, isVisible);

            var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.github.teru-p-q.assetstash/Editor/UXML/MemoCellTemplate.uxml");
            column.makeCell = () => template.Instantiate();
            column.bindCell = (e, i) =>
            {
                var item = stashTree.GetItemDataForIndex(i);
                if (item == null)
                {
                    return;
                }

                var label = FindLabelField(e);

                // 既に TextField がある場合は先に消す（再利用時のクリーン）
                var existingTf = stashTree.FindTextField(e);
                if (existingTf != null)
                {
                    existingTf.RemoveFromHierarchy();
                }

                if (editId == item.ID)
                {
                    if (label != null)
                    {
                        label.style.display = DisplayStyle.None;
                    }

                    var tf = new TextField();
                    tf.name = "InlineEdit";
                    tf.value = item.Memo;
                    tf.style.flexGrow = 1;
                    e.Add(tf);
                    var element = tf.Q(TextField.textInputUssName);
                    element?.Focus();

                    var color = EditorGUIUtility.isProSkin ? Color.white : Color.black;
                    element.style.color = new StyleColor(color);
                    // 必要なら背景やフォントも明示
                    element.style.unityTextAlign = TextAnchor.UpperLeft;

                    RegisterInlineEditKeys(tf, () => EndEdit(tf, item, tf.value), () => CancelEdit(tf));
                }
                else
                {
                    if (label != null)
                    {
                        label.style.display = DisplayStyle.Flex;
                        label.text = item.Memo;

                        label.RegisterCallback<MouseDownEvent>(me =>
                        {
                            if (me.button == (int)MouseButton.RightMouse)
                            {
                                var menu = new GenericMenu();
                                menu.AddItem(new GUIContent("Edit Memo"), false, () => BeginEdit(item.ID));
                                menu.ShowAsContext();
                            }

                            if (me.clickCount == 2)
                            {
                                me.StopImmediatePropagation();

                                if (item.IsGroup)
                                {
                                    BeginEdit(item.ID);
                                }
                                else
                                {
                                    AssetStashUtil.OpenAsset(item);
                                }
                                return;
                            }
                        });
                    }
                }
            };
        }

        public override void BeginEdit(int id) => BeginInlineEdit(ref editId, id);

        public override void EndEdit(VisualElement e, AssetData item, string newMemo)
        {
            if (item == null)
            {
                return;
            }

            item.Memo = newMemo;
            editId = -1;

            FinishInlineEdit(e, parent =>
            {
                var memoLabel = FindLabelField(parent);
                if (memoLabel != null)
                {
                    memoLabel.text = newMemo;
                    memoLabel.style.display = DisplayStyle.Flex;
                }
            });
        }

        public override void CancelEdit(VisualElement e)
        {
            editId = -1;

            FinishInlineEdit(e, parent =>
            {
                var memoLabel = FindLabelField(parent);
                if (memoLabel != null)
                {
                    memoLabel.style.display = DisplayStyle.Flex;
                }
            });
        }
    }
}
