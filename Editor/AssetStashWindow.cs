using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
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

        Object[] cachedDragObjects = new Object[0];
        string[] cachedDragPaths = new string[0];
        bool isPathEnabled;
        bool isGUIDEnabled;
        bool isMemoVisible;
        bool autoSave;

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
            treeItems = BuildList(items);
            if (stashTree == null)
            {
                return;
            }

            autoSave = false;
            stashTree.PathProperty.IsVisible = isPathEnabled;
            stashTree.GuidProperty.IsVisible = isGUIDEnabled;
            stashTree.MemoProperty.IsVisible = isMemoVisible;
            autoSave = true;

            stashTree.SetTreeItems(treeItems);
            stashTree.Rebuild();
        }

        void AddStash()
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
        }

        private void OnCreateGroupButton()
        {
            CurrentID++;

            var info = new AssetData()
            {
                Guid = "",
                ID = CurrentID,
                Name = "New Group",
                Memo = "",
                Type = "Group",
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
            Reset();
            SaveStash(assetsCache.Select(x => x).ToList());
            Reload();
        }

        void OnAddButton()
        {
            AddStash();
        }

        int[] pendingDraggedIds = null;

        public void CreateBookmarkGUI()
        {
            var root = rootVisualElement;

            var vitualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.github.teru-p-q.assetstash/Editor/UXML/AssetStashWindow.uxml");
            var uxmlRoot = vitualTreeAsset.Instantiate();
            root.Add(uxmlRoot);
            uxmlRoot.style.flexGrow = 1;

            uxmlRoot.Q<Button>("Add").clicked += OnAddButton;
            uxmlRoot.Q<Button>("CreateGroup").clicked += OnCreateGroupButton;
            uxmlRoot.Q<Button>("Reset").clicked += OnResetButton;

            stashTree = new (uxmlRoot.Q<MultiColumnTreeView>("StashTree"),
                uxmlRoot.Q<Toggle>("Path"),
                uxmlRoot.Q<Toggle>("GUID"),
                uxmlRoot.Q<Toggle>("Memo"));
            stashTree.SetTreeItems(treeItems);

            stashTree.CanStartDrag += (args) => true;
            stashTree.SetupDragAndDrop += args =>
            {
                pendingDraggedIds = args.selectedIds?.ToArray();
                return SetupDragAndDrop(args, pendingDraggedIds);
            };
            stashTree.DragAndDropUpdate += args => DragAndDropUpdate(args, pendingDraggedIds);
            stashTree.HandleDrop += args => HandleDrop(args, pendingDraggedIds);

            stashTree.ItemExpandedChanged += (item) =>
            {
                var i = assetsCache.First(x => x.ID == item.id);
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

            root.RegisterCallback<MouseUpEvent>(me =>
            {
                if (me.button == (int)MouseButton.RightMouse)
                {
                    var selectedItem = stashTree.GetItemDataForIndex(stashTree.SelectedIndex);
                    stashTree.SetSelectionById(selectedItem.ID);

                    Vector2 mousePos = me.mousePosition;
                    Rect menuRect = new Rect(mousePos, Vector2.zero);

                    EditorApplication.delayCall += () =>
                    {
                        var menu = new GenericMenu();
                        if (selectedItem == null)
                        {
                            return;
                        }

                        if (!selectedItem.IsGroup && !selectedItem.IsExternal)
                        {
                            var path = AssetStashUtil.GuidToPath(selectedItem.Guid);
                            if (!File.GetAttributes(path).HasFlag(FileAttributes.Directory))
                            {
                                menu.AddItem(new GUIContent($"{Path.GetFileNameWithoutExtension(selectedItem.Name)} を開く"), false, () => AssetStashUtil.OpenAsset(selectedItem));
                                menu.AddSeparator("");
                            }
                        }

                        if (selectedItem.IsExternal)
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

                        if (!selectedItem.IsGroup && !selectedItem.IsExternal)
                        {
                            menu.AddSeparator("");
                            menu.AddItem(new GUIContent("アセットの場所を示す"), false, () => AssetStashUtil.PingAsset(selectedItem));
                        }

                        menu.DropDown(menuRect);
                    };
                }
            });

            root.RegisterCallback<KeyDownEvent>(x =>
            {
                if (x.keyCode == KeyCode.F2)
                {
                    x.StopImmediatePropagation();
                    var item = stashTree.SelectedItem;
                    if (item.IsGroup)
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


            Reload();
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

                    if (cls != null && typeof(UnityEngine.MonoBehaviour).IsAssignableFrom(cls))
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

            // Set the payload
            DragAndDrop.objectReferences = objectRefs.ToArray();
            DragAndDrop.paths = paths.ToArray();
            DragAndDrop.SetGenericData("PendingDataID", draggedIds[0]);

            // Cache the payload for DragAndDropUpdate to use
            cachedDragObjects = objectRefs.ToArray();
            cachedDragPaths = paths.ToArray();

            // Return Copy mode - UIElements will handle the visual feedback
            return new StartDragArgs("Dragging Assets", DragVisualMode.Copy);
        }

        DragVisualMode DragAndDropUpdate(HandleDragAndDropArgs args, int[] draggedIds)
        {
            // Restore cached payload since UIElements/DragAndDrop API clears it
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

            int targetParent = args.parentId; // -1 = root
                                              // 例: 自分自身や自分の子にドロップしようとしていないかチェック
            if (!hasExternal && dragged != null && dragged.Any(id => id == targetParent)) // || IsDescendant(id, targetParent)))
            {
                return DragVisualMode.Rejected;
            }

            return DragVisualMode.Copy;
        }

        DragVisualMode HandleDrop(HandleDragAndDropArgs args, int[] draggedIds)
        {
            // Restore cached payload since UIElements/DragAndDrop API clears it
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

            // Clear the cache after drop
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
                    var parentItem = assetsCache.First(x => x.ID == parent.ID);
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
                    var parentItem = assetsCache.First(x => x.ID == args.parentId);
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
                    assetsCache.Remove(dragItem);
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
                        var parentItem = assetsCache.First(x => x.ID == parent.ID);
                        dragItem.ParentID = parent.ID;
                        assetsCache.Add(dragItem);
                        refreshItem = dragItem;
                    }
                    else
                    {
                        dragItem.ParentID = parent.ParentID;
                        assetsCache.Insert(insertIndex, (AssetData)dragItem.Clone());
                        refreshItem = dragItem;
                    }
                }
                else if (args.target == null)
                {
                    if (args.parentId != -1)
                    {
                        var parentItem = assetsCache.First(x => x.ID == args.parentId);
                        dragItem.ParentID = args.parentId;
                        assetsCache.Insert(insertIndex, (AssetData)dragItem.Clone());
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
}