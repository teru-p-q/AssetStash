using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuonLib.AssetStash
{
    public partial class AssetStashWindow : EditorWindow
    {
        List<TreeViewItemData<AssetData>> treeItems = new();
        AssetStashTree stashTree;

        int CurrentID = 0;
        List<AssetData> assetsCache;

        UnityEngine.Object[] cachedDragObjects = new UnityEngine.Object[0];
        string[] cachedDragPaths = new string[0];
        bool isPathEnabled;
        bool isGUIDEnabled;
        bool isMemoVisible;
        bool autoSave;

        ToolbarSearchField searchField;
        Button cleanupButton;
        string searchText = "";

        bool IsFiltering => !string.IsNullOrEmpty(searchText);

        void Reload()
        {
            CurrentID = 0;
            (isPathEnabled, isGUIDEnabled, isMemoVisible, assetsCache) = Bookmark.LoadPrefs();

            if (assetsCache.Count > 0)
            {
                CurrentID = assetsCache.Max(x => x.ID) + 1;
            }
            
            RebuildTree(assetsCache);
        }

        void RebuildTree(List<AssetData> items, AssetData refreshItem = null)
        {
            treeItems = BuildList(FilterAssets(items));
            UpdateCleanupButton();

            if (stashTree == null)
            {
                return;
            }

            autoSave = false;
            stashTree.PathProperty.IsVisible = isPathEnabled;
            stashTree.GuidProperty.IsVisible = isGUIDEnabled;
            stashTree.MemoProperty.IsVisible = isMemoVisible;
            autoSave = true;

            stashTree.ForceExpandAll = IsFiltering;
            stashTree.SetTreeItems(treeItems);
            stashTree.Rebuild();
        }

        bool AddStash()
        {
            bool changed = false;

            foreach (string assetGuid in Selection.assetGUIDs)
            {
                if (assetsCache.Any(x => x.Guid == assetGuid))
                {
                    continue;
                }

                AssetData parent = null;
                if (stashTree.SelectedIds.Count() > 0)
                {
                    var id = stashTree.SelectedIds.FirstOrDefault();
                    parent = assetsCache.First(x => x.ID == id);
                }

                Bookmark.Add(assetsCache, assetGuid, parent, ++CurrentID);

                changed = true;
            }

            if (changed)
            {
                SaveStash(assetsCache);
                RebuildTree(assetsCache);
            }

            return changed;
        }

        private void OnCreateGroupButton()
        {
            ClearSearch();

            CurrentID++;

            var info = new AssetData()
            {
                Guid = "",
                ID = CurrentID,
                Name = "New Group",
                Memo = "",
                Type = AssetData.GroupType,
                ParentID = -1,
                IsExpanded = false,
            };

            assetsCache.Add(info);
            //
            SaveStash(assetsCache);
            RebuildTree(assetsCache);

            stashTree.BeginNameEdit(info.ID);
        }

        void OnResetButton()
        {
            ClearSearch();
            Reset();
            SaveStash(assetsCache.Select(x => x).ToList());
            Reload();
        }

        void OnAddButton()
        {
            var cleared = ClearSearch();
            var added = AddStash();

            if (cleared && !added)
            {
                RebuildTree(assetsCache);
            }
        }

        int[] pendingDraggedIds = null;

        public void CreateBookmarkGUI()
        {
            var root = rootVisualElement;
            var uxmlRoot = InstantiateWindowLayout(root);

            SetupToolbarButtons(uxmlRoot);
            SetupStashTree(uxmlRoot);
            SetupSearchField(uxmlRoot);
            SetupDragAndDropHandlers();
            SetupTreeChangeHandlers();
            SetupDoubleClickToOpen(root);
            SetupContextMenu(root);
            SetupKeyboardShortcuts(root);

            Reload();
        }

        VisualElement InstantiateWindowLayout(VisualElement root)
        {
            var vitualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.github.teru-p-q.assetstash/Editor/UXML/AssetStashWindow.uxml");
            var uxmlRoot = vitualTreeAsset.Instantiate();
            root.Add(uxmlRoot);
            uxmlRoot.style.flexGrow = 1;
            return uxmlRoot;
        }

        void SetupToolbarButtons(VisualElement uxmlRoot)
        {
            uxmlRoot.Q<Button>("Add").clicked += OnAddButton;
            uxmlRoot.Q<Button>("CreateGroup").clicked += OnCreateGroupButton;
            uxmlRoot.Q<Button>("Reset").clicked += OnResetButton;

            cleanupButton = uxmlRoot.Q<Button>("Cleanup");
            if (cleanupButton != null)
            {
                cleanupButton.clicked += OnCleanupButton;
            }
        }

        void SetupStashTree(VisualElement uxmlRoot)
        {
            stashTree = new(uxmlRoot.Q<MultiColumnTreeView>("StashTree"),
                uxmlRoot.Q<Toggle>("Path"),
                uxmlRoot.Q<Toggle>("GUID"),
                uxmlRoot.Q<Toggle>("Memo"));
            stashTree.SetTreeItems(treeItems);
        }

        void SetupSearchField(VisualElement uxmlRoot)
        {
            searchField = uxmlRoot.Q<ToolbarSearchField>("Search");
            if (searchField == null)
            {
                return;
            }

            searchField.RegisterValueChangedCallback(evt =>
            {
                searchText = evt.newValue == null ? "" : evt.newValue.Trim();
                RebuildTree(assetsCache);
            });
        }

        void SetupDragAndDropHandlers()
        {
            stashTree.CanStartDrag += (args) => true;
            stashTree.SetupDragAndDrop += args =>
            {
                pendingDraggedIds = args.selectedIds?.ToArray();
                return SetupDragAndDrop(args, pendingDraggedIds);
            };
            stashTree.DragAndDropUpdate += args => DragAndDropUpdate(args, pendingDraggedIds);
            stashTree.HandleDrop += args => HandleDrop(args, pendingDraggedIds);
        }

        void SetupTreeChangeHandlers()
        {
            stashTree.ItemExpandedChanged += (item) =>
            {
                if (IsFiltering)
                {
                    return;
                }

                var i = assetsCache.FirstOrDefault(x => x.ID == item.id);
                if (i == null)
                {
                    return;
                }

                i.IsExpanded = item.isExpanded;
                SaveStash(assetsCache);
            };

            stashTree.OnChanged += () =>
            {
                if (autoSave)
                {
                    SaveStash(assetsCache);
                }
            };
        }

        void SetupDoubleClickToOpen(VisualElement root)
        {
            root.RegisterCallback<ClickEvent>(me =>
            {
                if (me.clickCount == 2)
                {
                    me.StopImmediatePropagation();
                    var selectedItem = stashTree.GetItemDataForIndex(stashTree.SelectedIndex);
                    AssetStashUtil.OpenAsset(selectedItem);
                    return;
                }
            });
        }

        void SetupContextMenu(VisualElement root)
        {
            root.RegisterCallback<MouseUpEvent>(me =>
            {
                if (me.button == (int)MouseButton.RightMouse)
                {
                    var selectedItem = stashTree.GetItemDataForIndex(stashTree.SelectedIndex);
                    stashTree.SetSelectionById(selectedItem.ID);

                    Vector2 mousePos = me.mousePosition;
                    Rect menuRect = new Rect(mousePos, Vector2.zero);

                    EditorApplication.delayCall += () => ShowContextMenu(selectedItem, menuRect);
                }
            });
        }

        void ShowContextMenu(AssetData selectedItem, Rect menuRect)
        {
            var menu = new GenericMenu();
            if (selectedItem == null)
            {
                return;
            }

            var isMissing = AssetStashUtil.IsMissing(selectedItem);

            if (!selectedItem.IsGroup && !selectedItem.IsExternal && !isMissing)
            {
                var path = AssetStashUtil.GuidToPath(selectedItem.Guid);
                if (!AssetDatabase.IsValidFolder(path))
                {
                    menu.AddItem(new GUIContent($"{Path.GetFileNameWithoutExtension(selectedItem.Name)} を開く"), false, () => AssetStashUtil.OpenAsset(selectedItem));
                    menu.AddSeparator("");
                }
            }

            if (selectedItem.IsExternal && !isMissing)
            {
                menu.AddItem(new GUIContent($"{Path.GetFileNameWithoutExtension(selectedItem.Name)} の場所をエクスプローラーで開く"), false, () => AssetStashUtil.OpenFolder(selectedItem));
                menu.AddSeparator("");
            }

            if (selectedItem.IsGroup)
            {
                menu.AddItem(new GUIContent("グループ名を編集"), false, () => stashTree.BeginNameEdit(selectedItem.ID));
            }
            menu.AddItem(new GUIContent("メモを編集"), false, () => stashTree.BeginMemoEdit(selectedItem.ID));
            menu.AddSeparator("");

            if (selectedItem.IsGroup)
            {
                menu.AddItem(new GUIContent($"{Path.GetFileNameWithoutExtension(selectedItem.Name)} を削除"), false, () => Delete(selectedItem));
            }
            else
            {
                menu.AddItem(new GUIContent($"{Path.GetFileNameWithoutExtension(selectedItem.Name)} の登録を解除"), false, () => Delete(selectedItem));
            }

            if (!selectedItem.IsGroup && !selectedItem.IsExternal && !isMissing)
            {
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("アセットの場所を示す"), false, () => AssetStashUtil.PingAsset(selectedItem));
            }

            menu.DropDown(menuRect);
        }

        void SetupKeyboardShortcuts(VisualElement root)
        {
            root.RegisterCallback<KeyDownEvent>(x =>
            {
                if (x.target is VisualElement ve && ve.GetFirstOfType<TextField>() != null)
                {
                    return;
                }

                if (x.keyCode == KeyCode.F2)
                {
                    x.StopImmediatePropagation();
                    var item = stashTree.SelectedItem;
                    if (item != null && item.IsGroup)
                    {
                        stashTree.BeginNameEdit(item.ID);
                    }
                }
                else if (x.keyCode == KeyCode.Delete || x.keyCode == KeyCode.Backspace)
                {
                    x.StopImmediatePropagation();
                    var item = stashTree.SelectedItem;
                    if (item != null)
                    {
                        Delete(item);
                    }
                }
            });
        }

        List<TreeViewItemData<AssetData>> BuildList(List<AssetData> assets)
        {
            var childItemsDict = new Dictionary<int, List<AssetData>>();

            foreach (var item in assets)
            {
                if (item.IsGroup)
                {
                    childItemsDict.Add(item.ID, new List<AssetData>());
                }
                else
                {
                    if (childItemsDict.ContainsKey(item.ParentID))
                    {
                        childItemsDict[item.ParentID].Add(item);
                    }
                    else
                    {
                        childItemsDict.Add(item.ID, new List<AssetData>());
                    }
                }
            }

            return childItemsDict.Select(x =>
            {
                var groupData = assets.First(a => a.ID == x.Key);
                var childItems = x.Value.Select(c => new TreeViewItemData<AssetData>(c.ID, c)).ToList();
                return new TreeViewItemData<AssetData>(groupData.ID, groupData, childItems);
            }).ToList();
        }

        #region Missing
        void UpdateCleanupButton()
        {
            if (cleanupButton == null)
            {
                return;
            }

            var count = assetsCache == null ? 0 : assetsCache.Count(AssetStashUtil.IsMissing);

            cleanupButton.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            cleanupButton.text = $"欠損を削除 ({count})";
        }

        void OnCleanupButton()
        {
            if (assetsCache == null)
            {
                return;
            }

            var missing = assetsCache.Where(AssetStashUtil.IsMissing).ToList();
            if (missing.Count == 0)
            {
                UpdateCleanupButton();
                return;
            }

            const int previewCount = 10;
            var names = string.Join("\n", missing.Take(previewCount).Select(x => x.IsExternal ? x.Name : $"{x.Name} ({x.Guid})"));
            if (missing.Count > previewCount)
            {
                names += $"\n... 他 {missing.Count - previewCount} 件";
            }

            if (!EditorUtility.DisplayDialog("欠損ブックマークの削除", $"参照先が見つからない {missing.Count} 件を削除します。\n\n{names}", "削除", "キャンセル"))
            {
                return;
            }

            ClearSearch();
            assetsCache.RemoveAll(AssetStashUtil.IsMissing);
            SaveStash(assetsCache);
            RebuildTree(assetsCache);
        }
        #endregion

        #region Search
        bool ClearSearch()
        {
            if (!IsFiltering)
            {
                return false;
            }

            searchText = "";
            searchField?.SetValueWithoutNotify("");
            return true;
        }

        List<AssetData> FilterAssets(List<AssetData> assets)
        {
            if (!IsFiltering || assets == null)
            {
                return assets;
            }

            var keywords = searchText.Split(new[] { ' ', '　' }, StringSplitOptions.RemoveEmptyEntries);
            if (keywords.Length == 0)
            {
                return assets;
            }

            var visibleIds = new HashSet<int>();
            var matchedGroupIds = new HashSet<int>();

            foreach (var item in assets)
            {
                if (!IsMatch(item, keywords))
                {
                    continue;
                }

                visibleIds.Add(item.ID);

                if (item.IsGroup)
                {
                    matchedGroupIds.Add(item.ID);
                }
                else if (item.ParentID != -1)
                {
                    visibleIds.Add(item.ParentID);
                }
            }

            return assets
                .Where(x => visibleIds.Contains(x.ID) || (!x.IsGroup && matchedGroupIds.Contains(x.ParentID)))
                .ToList();
        }

        static bool IsMatch(AssetData item, string[] keywords)
        {
            var name = item.Name ?? "";
            var memo = item.Memo ?? "";
            var path = item.IsGroup ? "" : (item.IsExternal ? name : AssetStashUtil.GuidToPath(item.Guid));

            foreach (var keyword in keywords)
            {
                if (!Contains(name, keyword)
                    && !Contains(path, keyword)
                    && !Contains(item.Guid, keyword)
                    && !Contains(memo, keyword))
                {
                    return false;
                }
            }

            return true;
        }

        static bool Contains(string source, string keyword)
            => !string.IsNullOrEmpty(source) && source.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
        #endregion

        void SaveStash(List<AssetData> assetData)
        {
            if (stashTree == null)
            {
                return;
            }
            Bookmark.SavePrefs(stashTree.PathProperty.IsVisible, stashTree.GuidProperty.IsVisible, stashTree.MemoProperty.IsVisible, assetData);
        }

        public void Delete(AssetData item)
        {
            assetsCache.RemoveAll(a => a.ID == item.ID || a.ParentID == item.ID);
            SaveStash(assetsCache);
            RebuildTree(assetsCache);
        }

        #region DnD
        StartDragArgs SetupDragAndDrop(SetupDragAndDropArgs args, int[] draggedIds)
        {
            var objectRefs = new List<UnityEngine.Object>();
            var paths = new List<string>();

            if (draggedIds == null || draggedIds.Length == 0 || assetsCache == null)
            {
                return new StartDragArgs("Dragging Assets", DragVisualMode.None);
            }

            var m = assetsCache.FirstOrDefault(x => x.ID == draggedIds[0]);

            if (m != null)
            {
                string path = AssetDatabase.GUIDToAssetPath(m.Guid);
                var mono = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

                if (mono != null)
                {
                    var cls = mono.GetClass();

                    if (cls != null && typeof(MonoBehaviour).IsAssignableFrom(cls))
                    {
                        objectRefs.Add(mono);
                    }
                    else
                    {
                        var main = AssetDatabase.LoadMainAssetAtPath(path);
                        if (main != null)
                        {
                            objectRefs.Add(main);
                        }
                    }
                }
                else
                {
                    var main = AssetDatabase.LoadMainAssetAtPath(path);
                    if (main != null)
                    {
                        objectRefs.Add(main);
                    }
                }

                paths.Add(path);
            }

            DragAndDrop.objectReferences = objectRefs.ToArray();
            DragAndDrop.paths = paths.ToArray();
            DragAndDrop.SetGenericData("PendingDataID", draggedIds[0]);

            cachedDragObjects = objectRefs.ToArray();
            cachedDragPaths = paths.ToArray();

            return new StartDragArgs("Dragging Assets", DragVisualMode.Copy);
        }

        DragVisualMode DragAndDropUpdate(HandleDragAndDropArgs args, int[] draggedIds)
        {
            if (IsFiltering)
            {
                return DragVisualMode.Rejected;
            }

            if (cachedDragObjects.Length > 0 && DragAndDrop.objectReferences.Length == 0)
            {
                DragAndDrop.objectReferences = cachedDragObjects;
                DragAndDrop.paths = cachedDragPaths;
            }

            var dragged = draggedIds;
            bool hasExternal = DragAndDrop.paths != null && DragAndDrop.paths.Length > 0;
            bool hasObjectRefs = DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0;

            if ((dragged == null || dragged.Length == 0) && !hasExternal && !hasObjectRefs)
            {
                return DragVisualMode.Rejected;
            }

            int targetParent = args.parentId;
            if (!hasExternal && dragged != null && dragged.Any(id => id == targetParent))
            {
                return DragVisualMode.Rejected;
            }

            if (!hasExternal)
            {
                var f = assetsCache.Find(x => x.ID == dragged[0]);
                if (f != null && f.IsGroup && targetParent != -1)
                {
                    return DragVisualMode.Rejected;
                }
            }

            return DragVisualMode.Copy;
        }

        DragVisualMode HandleDrop(HandleDragAndDropArgs args, int[] draggedIds)
        {
            if (IsFiltering)
            {
                return DragVisualMode.Rejected;
            }

            if (cachedDragObjects.Length > 0 && DragAndDrop.objectReferences.Length == 0)
            {
                DragAndDrop.objectReferences = cachedDragObjects;
                DragAndDrop.paths = cachedDragPaths;
            }

            var dragged = draggedIds;
            bool hasExternalDrop = DragAndDrop.paths != null && DragAndDrop.paths.Length > 0;
            if ((dragged == null || dragged.Length == 0) && !hasExternalDrop)
            {
                return DragVisualMode.Rejected;
            }

            if (dragged != null && dragged.Length > 0)
            {
                OnDrop(args, dragged);
            }
            else if (hasExternalDrop)
            {
                OnDrop(args, DragAndDrop.paths);
            }
            pendingDraggedIds = null;

            cachedDragObjects = new UnityEngine.Object[0];
            cachedDragPaths = new string[0];

            DragAndDrop.AcceptDrag();

            return DragVisualMode.Move;
        }

        void OnDrop(HandleDragAndDropArgs args, int[] dragged)
        {
            var targetItem = stashTree.GetItemDataForIndex(args.insertAtIndex);
            var insertIndex = targetItem == null ? assetsCache.Count() : assetsCache.FindIndex(x => x.ID == targetItem.ID);

            AssetData refreshItem = null;
            if (args.target != null)
            {
                var parent = (AssetData)args.target;
                var dragItem = assetsCache.First(x => x.ID == dragged[0]);

                if (parent.IsGroup)
                {
                    assetsCache.Remove(dragItem);
                    dragItem.ParentID = parent.ID;
                    assetsCache.Add(dragItem);
                    refreshItem = dragItem;
                }
                else
                {
                    dragItem.ParentID = parent.ParentID;
                    assetsCache.Insert(insertIndex, (AssetData)dragItem.Clone());
                    assetsCache.Remove(dragItem);
                    refreshItem = dragItem;
                }
            }
            else if (args.target == null)
            {
                if (args.parentId != -1)
                {
                    var dragItem = assetsCache.First(x => x.ID == dragged[0]);
                    dragItem.ParentID = args.parentId;
                    assetsCache.Insert(insertIndex, (AssetData)dragItem.Clone());
                    assetsCache.Remove(dragItem);
                    refreshItem = null;
                }
                else
                {
                    var dragItem = assetsCache.First(x => x.ID == dragged[0]);
                    dragItem.ParentID = -1;
                    assetsCache.Insert(insertIndex, (AssetData)dragItem.Clone());

                    if (dragItem.IsGroup)
                    {
                        var childItems = assetsCache.Where(x => x.ParentID == dragItem.ID).ToArray();
                        for (var i = 0; i < childItems.Length; i++)
                        {
                            assetsCache.Insert(insertIndex + i + 1, childItems[i]);
                        }

                        assetsCache.Remove(dragItem);
                        foreach (var i in childItems)
                        {
                            assetsCache.Remove(i);
                        }
                    }
                    else
                    {
                        assetsCache.Remove(dragItem);
                    }
                    refreshItem = null;
                }
            }
            SaveStash(assetsCache);
            RebuildTree(assetsCache, refreshItem);
        }

        void OnDrop(HandleDragAndDropArgs args, string[] draggedPath)
        {
            AssetData refreshItem = null;

            var targetItem = stashTree.GetItemDataForIndex(args.insertAtIndex);
            var insertIndex = targetItem == null ? args.insertAtIndex : assetsCache.FindIndex(x => x.ID == targetItem.ID);

            for (var i = 0; i < draggedPath.Length; i++)
            {
                var dragItem = Bookmark.CreateFromPath(draggedPath[i], ++CurrentID);
                if (dragItem.Guid != "" && assetsCache.Any(x => x.Guid == dragItem.Guid))
                {
                    CurrentID--;
                    stashTree.SetSelectionById(dragItem.ID);
                    continue;
                }

                if (args.target != null)
                {
                    var parent = (AssetData)args.target;

                    if (parent.IsGroup)
                    {
                        dragItem.ParentID = parent.ID;
                        assetsCache.Add(dragItem);
                        refreshItem = dragItem;
                    }
                    else
                    {
                        dragItem.ParentID = parent.ParentID;
                        assetsCache.Insert(insertIndex + 1, (AssetData)dragItem.Clone());
                        refreshItem = dragItem;
                    }
                }
                else
                {
                    if (args.parentId != -1)
                    {
                        var parentItem = assetsCache.First(x => x.ID == args.parentId);
                        dragItem.ParentID = args.parentId;

                        if (args.dropPosition == DragAndDropPosition.OutsideItems)
                        {
                            var parentIndex = assetsCache.FindIndex(x => x.ID == parentItem.ID);
                            var parentVisibleIndex = assetsCache.FindVisibleIndexUntil(x => x.ID == parentItem.ID);
                            assetsCache.Insert(parentIndex + insertIndex - parentVisibleIndex, (AssetData)dragItem.Clone());
                        }
                        else
                        {
                            assetsCache.Insert(insertIndex, (AssetData)dragItem.Clone());
                        }
                        refreshItem = null;
                    }
                    else
                    {
                        dragItem.ParentID = -1;
                        assetsCache.Insert(insertIndex, (AssetData)dragItem.Clone());
                        refreshItem = null;
                    }
                }
            }
            SaveStash(assetsCache);
            RebuildTree(assetsCache, refreshItem);
        }
        #endregion

    }

    static class ListAssetDataExtensions
    {
        public static int FindVisibleIndexUntil(this List<AssetData> list, Func<AssetData, bool> predicate)
        {
            var visibleIndex = 0;
            var count = list.Count;

            for (var i = 0; i < count; i++)
            {
                var item = list[i];

                if (predicate(item))
                {
                    break;
                }

                if (item.IsGroup)
                {
                    var childCount = CountVisibleChildrenUntil(list, i + 1, predicate);

                    if (!item.IsExpanded)
                    {
                        i += childCount;
                    }
                }
                visibleIndex++;
            }

            return visibleIndex;

            static int CountVisibleChildrenUntil(List<AssetData> list, int startIndex, Func<AssetData, bool> predicate)
            {
                var count = 0;
                var listCount = list.Count;
                var prentID = list[startIndex].ParentID;

                for (var i = startIndex; i < listCount; i++)
                {
                    var item = list[i];

                    if (item.IsGroup || prentID != item.ParentID)
                    {
                        break;
                    }

                    if (predicate(item))
                    {
                        break;
                    }

                    count++;
                }

                return count;
            }
        }
    }
}