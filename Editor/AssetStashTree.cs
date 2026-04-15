using KuonLib.AssetStash.Properties;
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace KuonLib.AssetStash
{
    public class AssetStashTree : IDisposable
    {
        MultiColumnTreeView treeView;
        List<TreeViewItemData<AssetData>> treeItems = new();

        public NameProperty NameProperty;
        public MemoProperty MemoProperty;
        public PathProperty PathProperty;
        public GUIDProperty GuidProperty;

        public event Func<CanStartDragArgs, bool> CanStartDrag
        {
            add => treeView.canStartDrag += value;
            remove => treeView.canStartDrag -= value;
        }

        public event Func<SetupDragAndDropArgs, StartDragArgs> SetupDragAndDrop
        {
            add => treeView.setupDragAndDrop += value;
            remove => treeView.setupDragAndDrop -= value;
        }

        public event Func<HandleDragAndDropArgs, DragVisualMode> DragAndDropUpdate
        {
            add => treeView.dragAndDropUpdate += value;
            remove => treeView.dragAndDropUpdate -= value;
        }

        public event Func<HandleDragAndDropArgs, DragVisualMode> HandleDrop
        {
            add => treeView.handleDrop += value;
            remove => treeView.handleDrop -= value;
        }

        public event Action<TreeViewExpansionChangedArgs> ItemExpandedChanged
        {
            add => treeView.itemExpandedChanged += value;
            remove => treeView.itemExpandedChanged -= value;
        }

        event Action onChanged;
        public event Action OnChanged
        {
            add => onChanged += value;
            remove => onChanged -= value;
        }

        public AssetStashTree(MultiColumnTreeView multiColumnTreeView, Toggle path, Toggle guid, Toggle memo)
        {
            treeView = multiColumnTreeView;

            treeView.fixedItemHeight = 18;
            treeView.virtualizationMethod = CollectionVirtualizationMethod.FixedHeight;
            treeView.SetRootItems(treeItems);
            treeView.reorderable = true;

            NameProperty = new(this);
            NameProperty.Create(NameColumn, null, true);
            NameProperty.OnChanged += x => VisibleChanged(x);

            PathProperty = new(this);
            PathProperty.Create(PathColumn, path, true);
            PathProperty.OnChanged += x => VisibleChanged(x);

            GuidProperty = new(this);
            GuidProperty.Create(GuidColumn, guid, true);
            GuidProperty.OnChanged += x => VisibleChanged(x);

            MemoProperty = new(this); 
            MemoProperty.Create(MemoColumn, memo, true);
            MemoProperty.OnChanged += x => VisibleChanged(x);
        }

        public void Dispose()
        {
            NameProperty.OnChanged -= x => VisibleChanged(x);
            PathProperty.OnChanged -= x => VisibleChanged(x);
            GuidProperty.OnChanged -= x => VisibleChanged(x);
            MemoProperty.OnChanged -= x => VisibleChanged(x);
        }

        void VisibleChanged(bool newValue)
        {
            onChanged?.Invoke();
        }

        public void SetTreeItems(List<TreeViewItemData<AssetData>> items)
        {
            treeItems = items;
            treeView.SetRootItems(treeItems);
        }

        public void Rebuild()
        {
            treeView.Rebuild();

            foreach (var item in treeItems)
            {
                if (item.data.IsExpanded)
                {
                    treeView.ExpandItem(item.data.ID);
                }
                else
                {
                    treeView.CollapseItem(item.data.ID);
                }
            }
        }

        public void MarkDirtyRepaint() => treeView.MarkDirtyRepaint();
        public void Focus() => treeView.Focus();

        public AssetData SelectedItem => (AssetData)treeView.selectedItem;
        public IEnumerable<int> SelectedIds => treeView.selectedIds;
        public int SelectedIndex => treeView.selectedIndex;

        Column NameColumn => treeView.columns["Name"];
        Column PathColumn => treeView.columns["Path"];
        Column GuidColumn => treeView.columns["GUID"];
        Column MemoColumn => treeView.columns["Memo"];

        public void BeginNameEdit(int id) => NameProperty.BeginEdit(id);
        public void BeginMemoEdit(int id) => MemoProperty.BeginEdit(id);

        public AssetData GetItemDataForIndex(int index) => treeView.GetItemDataForIndex<AssetData>(index);
        public void SetSelectionById(int id) => treeView.SetSelectionById(id);

        public TextField FindTextField(VisualElement ve) => ve.Query<TextField>("InlineEdit");
        public Label FindLabelField(VisualElement ve) => ve.Query<Label>("Memo");
    }
}
