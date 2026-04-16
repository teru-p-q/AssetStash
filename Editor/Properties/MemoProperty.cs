using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuonLib.AssetStash.Properties
{
    public class MemoProperty : StashProperty
    {
        AssetStashTree stashTree;
        int editId = -1;

        public MemoProperty(AssetStashTree tree)
        {
            stashTree = tree;
        }

        public override void Create(Column column, Toggle toggle, bool isVisible)
        {
            this.column = column;
            this.toggle = toggle;
            this.IsVisible = isVisible;

            var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.github.teru-p-q.assetstash/Editor/UXML/MemoCellTemplate.uxml");
            column.makeCell = () => template.Instantiate();
            column.bindCell = (e, i) =>
            {
                var item = stashTree.GetItemDataForIndex(i);
                if (item == null)
                {
                    return;
                }

                var label = stashTree.FindLabelField(e);

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

                    tf.RegisterCallback<KeyDownEvent>(
                        ke =>
                        {
                            if (ke.keyCode == KeyCode.Return || ke.keyCode == KeyCode.KeypadEnter)
                            {
                                EndEdit(tf, item, tf.value);
                                ke.StopImmediatePropagation();
                            }
                            else if (ke.keyCode == KeyCode.Escape)
                            {
                                CancelEdit(tf);
                                ke.StopImmediatePropagation();
                            }
                        });

                    tf.RegisterCallback<FocusOutEvent>(fe =>
                    {
                        EndEdit(tf, item, tf.value);
                    });
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

            toggle.RegisterValueChangedCallback(evt =>
            {
                IsVisible = evt.newValue;
            });
            AssetStashUtil.SetDefaultToggleStyle(toggle);
        }

        public override void BeginEdit(int id)
        {
            editId = id;
            if (stashTree != null)
            {
                stashTree.Rebuild();
                stashTree.MarkDirtyRepaint();
            }
        }

        public override void EndEdit(VisualElement e, AssetData item, string newMemo)
        {
            if (item == null)
            {
                return;
            }

            item.Memo = newMemo;
            editId = -1;

            if (stashTree == null)
            {
                return;
            }

            try
            {
                var tf = stashTree.FindTextField(e);
                var parent = tf.parent;
                if (parent != null)
                {
                    var memoLabel = stashTree.FindLabelField(parent);
                    if (memoLabel != null)
                    {
                        memoLabel.text = newMemo;
                        memoLabel.style.display = DisplayStyle.Flex;
                    }
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

        public override void CancelEdit(VisualElement e)
        {
            editId = -1;
            if (stashTree != null)
            {
                try
                {
                    var tf = stashTree.FindTextField(e);
                    var parent = tf.parent;
                    if (parent != null)
                    {
                        var memoLabel = stashTree.FindLabelField(parent);
                        if (memoLabel != null)
                        {
                            memoLabel.style.display = DisplayStyle.Flex;
                        }
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
        }
    }
}
