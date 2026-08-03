using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuonLib.AssetStash.Properties
{
    public class NameProperty : StashProperty
    {
        int editId = -1;

        public NameProperty(AssetStashTree tree) : base(tree) { }

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
                else if (AssetStashUtil.IsMissing(item))
                {
                    icon.image = GetMissingIcon()?.image as Texture2D;
                }
                else if (item.IsSceneObject)
                {
                    icon.image = GetSceneObjectIcon()?.image as Texture2D;
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

        Label FindLabelField(VisualElement ve) => ve.Query<Label>("Name");
        Image FindIconField(VisualElement ve) => ve.Query<Image>("Icon");

        GUIContent GetFolderIcon() => EditorGUIUtility.IconContent("d_FolderFavorite Icon");
        GUIContent GetDefaultIcon() => EditorGUIUtility.IconContent("DefaultAsset Icon");
        GUIContent GetMissingIcon() => EditorGUIUtility.IconContent("console.warnicon.sml");
        GUIContent GetSceneObjectIcon() => EditorGUIUtility.IconContent("GameObject Icon");

        static readonly Color MissingColor = new Color(0.85f, 0.45f, 0.4f);

        public override void Create(Column column, Toggle toggle, bool isVisible)
        {
            SetupColumn(column, toggle, isVisible);

            var template = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.github.teru-p-q.assetstash/Editor/UXML/NameCellTemplate.uxml");
            column.makeCell = () => template.Instantiate();
            column.bindCell = (e, i) =>
            {
                var item = stashTree.GetItemDataForIndex(i);
                if (item == null)
                {
                    return;
                }

                // icon
                SetNameIcon(e, item);

                var isMissing = AssetStashUtil.IsMissing(item);
                e.tooltip = isMissing ? AssetStashUtil.GetMissingTooltip(item) : "";

                var nameLabel = FindLabelField(e);

                // 既に TextField がある場合は先に消す（再利用時のクリーン）
                var existingTf = stashTree.FindTextField(e);
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

                    RegisterInlineEditKeys(tf, () => EndEdit(tf, item, tf.value), () => CancelEdit(tf));
                }
                else
                {
                    if (nameLabel != null)
                    {
                        nameLabel.style.display = DisplayStyle.Flex;
                        nameLabel.text = item.IsExternal ? Path.GetFileName(item.Name) : item.Name;
                        nameLabel.style.color = isMissing ? new StyleColor(MissingColor) : new StyleColor(StyleKeyword.Null);
                    }
                }
            };
        }

        public override void BeginEdit(int id) => BeginInlineEdit(ref editId, id);

        public override void EndEdit(VisualElement e, AssetData item, string newText)
        {
            if (item == null || editId != item.ID)
            {
                return;
            }

            var changed = item.Name != newText;

            item.Name = newText;
            editId = -1;

            FinishInlineEdit(e, parent =>
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
            });

            if (changed)
            {
                stashTree.NotifyItemEdited();
            }
        }

        public override void CancelEdit(VisualElement e)
        {
            editId = -1;

            FinishInlineEdit(e, parent =>
            {
                var nameLabel = FindLabelField(parent);
                if (nameLabel != null)
                {
                    nameLabel.style.display = DisplayStyle.Flex;
                }
            });
        }
    }
}
