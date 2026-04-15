using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuonLib.AssetStash.Properties
{
    public class NameProperty : StashProperty
    {
        AssetStashTree stashTree;
        int editId = -1;

        public NameProperty(AssetStashTree tree)
        {
            stashTree = tree;
        }

        void SetNameIcon(VisualElement e, AssetData item)
        {
            var icon = FindIconField(e);
            if (icon != null)
            {
                if (editId == -1)
                {
                    icon.style.display = DisplayStyle.Flex;
                }
                else
                {
                    icon.style.display = (editId == item.ID) ? DisplayStyle.None : DisplayStyle.Flex;
                }

                if (item.IsGroup)
                {
                    var folderIcon = GetFolderIcon();
                    icon.image = (Texture2D)folderIcon.image;
                }
                else
                {
                    var tex = AssetDatabase.GetCachedIcon(AssetStashUtil.GuidToPath(item.Guid)) as Texture2D;
                    if (tex == null)
                    {
                        var fb = GetDefaultIcon();
                        tex = fb?.image as Texture2D;
                    }
                    icon.image = tex;
                }
            }
        }

        TextField FindTextField(VisualElement ve) => ve.Query<TextField>("InlineEdit");
        Label FindLabelField(VisualElement ve) => ve.Query<Label>("Name");
        Image FindIconField(VisualElement ve) => ve.Query<Image>("Icon");

        GUIContent GetFolderIcon() => EditorGUIUtility.IconContent("d_FolderFavorite Icon");
        GUIContent GetDefaultIcon() => EditorGUIUtility.IconContent("DefaultAsset Icon");

        public override void Create(Column column, Toggle toggle, bool isVisible)
        {
            this.column = column;
            this.toggle = toggle;
            IsVisible = isVisible;

            column.bindCell = (e, i) =>
            {
                var item = stashTree.GetItemDataForIndex(i);
                if (item == null)
                {
                    return;
                }

                // icon
                SetNameIcon(e, item);

                var nameLabel = FindLabelField(e);

                // 既に TextField がある場合は先に消す（再利用時のクリーン）
                var existingTf = FindTextField(e);
                if (existingTf != null)
                {
                    existingTf.RemoveFromHierarchy();
                }

                if (editId == item.ID)
                {
                    if (nameLabel != null)
                    {
                        nameLabel.style.display = DisplayStyle.None;
                    }

                    var tf = new TextField();
                    tf.name = "InlineEdit";
                    tf.value = item.Name;
                    tf.style.flexGrow = 1;
                    e.Add(tf);
                    tf.Q(TextField.textInputUssName)?.Focus();

                    tf.RegisterCallback<KeyDownEvent>(
                        ke => {
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
                    if (nameLabel != null)
                    {
                        nameLabel.style.display = DisplayStyle.Flex;
                        nameLabel.text = item.Type == "external" ? Path.GetFileName(item.Name) : item.Name;

                        nameLabel.RegisterCallback<MouseDownEvent>(me =>
                        {
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

        public override void BeginEdit(int id)
        {
            editId = id;
            if (stashTree != null)
            {
                stashTree.Rebuild();
                stashTree.MarkDirtyRepaint();
            }
        }

        public override void EndEdit(VisualElement e, AssetData item, string newText)
        {
            if (item == null)
            {
                return;
            }

            item.Name = newText;
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
                    var nameLabel = FindLabelField(parent);
                    if (nameLabel != null)
                    {
                        nameLabel.text = newText;
                        nameLabel.style.display = DisplayStyle.Flex;
                    }

                    var iconRest = FindIconField(parent);
                    if (iconRest != null)
                    {
                        Texture2D tex = null;
                        if (!string.IsNullOrEmpty(item.Guid))
                        {
                            tex = AssetDatabase.GetCachedIcon(AssetStashUtil.GuidToPath(item.Guid)) as Texture2D;
                        }

                        if (tex == null)
                        {
                            var folderIcon = GetFolderIcon();
                            tex = folderIcon.image as Texture2D;
                        }
                        iconRest.image = tex;
                        iconRest.style.display = DisplayStyle.Flex;
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
                        var nameLabel = FindLabelField(parent);
                        if (nameLabel != null)
                        {
                            nameLabel.style.display = DisplayStyle.Flex;
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
