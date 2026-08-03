using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.ShortcutManagement;
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

        // ドラッグ情報は DragAndDrop の generic data に持たせる（ドラッグ単位で破棄されるため状態が残らない）
        const string DraggedIdsKey = "AssetStash.DraggedIds";
        const string DraggedObjectsKey = "AssetStash.DraggedObjects";
        const string DraggedPathsKey = "AssetStash.DraggedPaths";

        bool isPathEnabled;
        bool isGUIDEnabled;
        bool isMemoVisible;
        bool autoSave;

        ToolbarSearchField searchField;
        Button cleanupButton;
        string searchText = "";

        readonly UndoHistory undoHistory = new();
        List<AssetData> editSnapshot;

        // 項目 ID -> 最後に表示した状態。アセットの移動 / 削除 / 復活の検出に使う
        // （同じシーンの複数オブジェクトは GUID が重複するため、キーは ID にする）
        readonly Dictionary<int, string> resolvedPaths = new();

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

        public static void RefreshOpenWindows()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<AssetStashWindow>())
            {
                window.RefreshFromAssetDatabase();
            }
        }

        void RefreshFromAssetDatabase()
        {
            if (assetsCache == null || stashTree == null)
            {
                return;
            }

            var renamed = false;
            var moved = false;

            foreach (var item in assetsCache)
            {
                if (item.IsGroup || item.IsExternal || string.IsNullOrEmpty(item.Guid))
                {
                    continue;
                }

                var state = ResolveState(item);

                if (!resolvedPaths.TryGetValue(item.ID, out var previous) || previous != state)
                {
                    moved = true;
                }

                // シーン内オブジェクトの Guid はシーンを指すので、シーンアセット名で上書きしてはいけない
                if (item.IsSceneObject)
                {
                    renamed |= RefreshSceneObjectName(item);
                    continue;
                }

                var path = AssetStashUtil.GuidToPath(item.Guid);
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null || item.Name == asset.name)
                {
                    continue;
                }

                item.Name = asset.name;
                renamed = true;
            }

            if (!renamed && !moved)
            {
                return;
            }

            if (renamed)
            {
                SaveStash(assetsCache);
            }

            RebuildTree(assetsCache);
        }

        void CacheResolvedPaths(List<AssetData> items)
        {
            resolvedPaths.Clear();

            if (items == null)
            {
                return;
            }

            foreach (var item in items)
            {
                if (item.IsGroup || item.IsExternal || string.IsNullOrEmpty(item.Guid))
                {
                    continue;
                }

                resolvedPaths[item.ID] = ResolveState(item);
            }
        }

        // 削除されてもパスは古い値のまま残るため、実在の有無まで含めて状態とする
        static string ResolveState(AssetData item)
        {
            return AssetStashUtil.IsMissing(item) ? "" : AssetStashUtil.GuidToPath(item.Guid);
        }

        // シーンの開閉で解決できるかが変わるため、開いているときだけ名前を取り直す
        static bool RefreshSceneObjectName(AssetData item)
        {
            var sceneObject = AssetStashUtil.ResolveSceneObject(item);
            if (sceneObject == null || item.Name == sceneObject.name)
            {
                return false;
            }

            item.Name = sceneObject.name;
            return true;
        }

        void RefreshSceneObjects()
        {
            if (assetsCache == null || stashTree == null || !assetsCache.Any(x => x.IsSceneObject))
            {
                return;
            }

            // 購読順に依存しないよう、参照し直す前に自分でキャッシュを捨てる
            AssetStashUtil.ClearSceneObjectCache();

            var renamed = false;
            foreach (var item in assetsCache.Where(x => x.IsSceneObject))
            {
                renamed |= RefreshSceneObjectName(item);
            }

            if (renamed)
            {
                SaveStash(assetsCache);
            }

            RebuildTree(assetsCache);
        }

        void RebuildTree(List<AssetData> items, AssetData refreshItem = null)
        {
            treeItems = BuildList(FilterAssets(items));
            CacheResolvedPaths(items);
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
            var before = UndoHistory.CreateSnapshot(assetsCache);

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

            changed |= AddSceneObjects();

            if (changed)
            {
                undoHistory.Push(before);
                SaveStash(assetsCache);
                RebuildTree(assetsCache);
            }

            return changed;
        }

        bool AddSceneObjects()
        {
            var changed = false;

            foreach (var go in Selection.gameObjects)
            {
                if (!Bookmark.CanBookmarkSceneObject(go))
                {
                    continue;
                }

                var info = Bookmark.CreateFromSceneObject(go, CurrentID + 1);
                if (assetsCache.Any(x => x.IsSceneObject && x.GlobalId == info.GlobalId))
                {
                    continue;
                }

                CurrentID++;
                InsertUnderSelection(info);
                changed = true;
            }

            return changed;
        }

        // 選択中の項目の直後（グループが選択されていればその配下）に差し込む
        void InsertUnderSelection(AssetData info)
        {
            var parent = SelectedParent();

            if (parent == null)
            {
                assetsCache.Add(info);
                return;
            }

            info.ParentID = parent.IsGroup ? parent.ID : parent.ParentID;
            assetsCache.Insert(assetsCache.FindIndex(x => x.ID == parent.ID) + 1, info);
        }

        AssetData SelectedParent()
        {
            var id = stashTree.SelectedIds.FirstOrDefault();
            return stashTree.SelectedIds.Any() ? assetsCache.FirstOrDefault(x => x.ID == id) : null;
        }

        private void OnCreateGroupButton()
        {
            ClearSearch();

            var before = UndoHistory.CreateSnapshot(assetsCache);
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
            undoHistory.Push(before);
            SaveStash(assetsCache);
            RebuildTree(assetsCache);

            stashTree.BeginNameEdit(info.ID);
        }

        void OnResetButton()
        {
            var count = assetsCache == null ? 0 : assetsCache.Count;
            if (count > 0 && !EditorUtility.DisplayDialog(
                "リセット",
                $"登録されている {count} 件のブックマークをすべて削除します。\n\nこの操作は Ctrl+Z で元に戻せます。",
                "削除",
                "キャンセル"))
            {
                return;
            }

            var before = UndoHistory.CreateSnapshot(assetsCache);

            ClearSearch();
            Reset();
            SaveStash(assetsCache.Select(x => x).ToList());
            Reload();

            undoHistory.Push(before);
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
            stashTree.SetupDragAndDrop += args => SetupDragAndDrop(args, args.selectedIds?.ToArray());
            stashTree.DragAndDropUpdate += args => DragAndDropUpdate(args, GetDraggedIds());
            stashTree.HandleDrop += args => HandleDrop(args, GetDraggedIds());
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

            stashTree.OnItemEditBegin += () => editSnapshot = UndoHistory.CreateSnapshot(assetsCache);

            stashTree.OnItemEdited += () =>
            {
                if (editSnapshot != null)
                {
                    undoHistory.Push(editSnapshot);
                    editSnapshot = null;
                }

                SaveStash(assetsCache);
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
                    var selectedItems = stashTree.SelectedItems.ToList();
                    var selectedItem = stashTree.GetItemDataForIndex(stashTree.SelectedIndex);

                    // 複数選択中は選択を維持し、一括操作のメニューを出す
                    if (selectedItems.Count <= 1 && selectedItem != null)
                    {
                        stashTree.SetSelectionById(selectedItem.ID);
                    }

                    // DropDown の座標は「呼び出し時点でアクティブなウィンドウ」を基準に解決されるため、
                    // delayCall で遅らせるとイベント発生時の基準を失い、表示位置がずれる
                    me.StopImmediatePropagation();
                    ShowContextMenu(selectedItem, selectedItems, new Rect(me.mousePosition, Vector2.zero));
                }
            });
        }

        void ShowContextMenu(AssetData selectedItem, List<AssetData> selectedItems, Rect menuRect)
        {
            if (selectedItems != null && selectedItems.Count > 1)
            {
                ShowMultiSelectionContextMenu(selectedItems, menuRect);
                return;
            }

            var menu = new GenericMenu();
            if (selectedItem == null)
            {
                return;
            }

            var isMissing = AssetStashUtil.IsMissing(selectedItem);
            var displayName = Path.GetFileNameWithoutExtension(selectedItem.Name);
            var path = AssetStashUtil.GetPath(selectedItem);

            if (selectedItem.IsSceneObject && !isMissing)
            {
                menu.AddItem(new GUIContent($"{displayName} を選択"), false, () => AssetStashUtil.SelectSceneObject(selectedItem));
                menu.AddSeparator("");
            }
            else if (!selectedItem.IsGroup && !selectedItem.IsExternal && !isMissing && !AssetStashUtil.IsFolder(selectedItem))
            {
                menu.AddItem(new GUIContent($"{displayName} を開く"), false, () => AssetStashUtil.OpenAsset(selectedItem));

                if (AssetStashUtil.IsScene(selectedItem))
                {
                    menu.AddItem(new GUIContent($"{displayName} を加算で開く"), false, () => AssetStashUtil.OpenSceneAdditive(selectedItem));
                }

                menu.AddSeparator("");
            }

            if (selectedItem.IsGroup)
            {
                menu.AddItem(new GUIContent("グループ名を編集"), false, () => stashTree.BeginNameEdit(selectedItem.ID));
            }
            menu.AddItem(new GUIContent("メモを編集"), false, () => stashTree.BeginMemoEdit(selectedItem.ID));

            if (!selectedItem.IsGroup && !selectedItem.IsSceneObject && !isMissing)
            {
                menu.AddSeparator("");

                if (!selectedItem.IsExternal)
                {
                    menu.AddItem(new GUIContent("アセットの場所を示す"), false, () => AssetStashUtil.PingAsset(selectedItem));
                }

                menu.AddItem(new GUIContent("エクスプローラーで開く"), false, () => AssetStashUtil.OpenFolder(selectedItem));
            }

            if (!selectedItem.IsGroup && (!string.IsNullOrEmpty(path) || !string.IsNullOrEmpty(selectedItem.Guid)))
            {
                menu.AddSeparator("");

                if (!string.IsNullOrEmpty(path))
                {
                    var pathLabel = selectedItem.IsSceneObject ? "シーンのパスをコピー" : "パスをコピー";
                    menu.AddItem(new GUIContent(pathLabel), false, () => AssetStashUtil.CopyToClipboard(path));
                }

                if (selectedItem.IsSceneObject)
                {
                    if (!string.IsNullOrEmpty(selectedItem.GlobalId))
                    {
                        menu.AddItem(new GUIContent("GlobalObjectId をコピー"), false, () => AssetStashUtil.CopyToClipboard(selectedItem.GlobalId));
                    }
                }
                // 欠損項目でも GUID は復旧の手掛かりになるためコピーできるようにする
                else if (!selectedItem.IsExternal && !string.IsNullOrEmpty(selectedItem.Guid))
                {
                    menu.AddItem(new GUIContent("GUID をコピー"), false, () => AssetStashUtil.CopyToClipboard(selectedItem.Guid));
                }
            }

            menu.AddSeparator("");

            if (selectedItem.IsGroup)
            {
                menu.AddItem(new GUIContent($"{displayName} を削除"), false, () => Delete(selectedItem));
            }
            else
            {
                menu.AddItem(new GUIContent($"{displayName} の登録を解除"), false, () => Delete(selectedItem));
            }

            AddUndoMenuItem(menu);

            menu.DropDown(menuRect);
        }

        void ShowMultiSelectionContextMenu(List<AssetData> selectedItems, Rect menuRect)
        {
            var menu = new GenericMenu();

            var alive = selectedItems.Where(x => !x.IsGroup && !AssetStashUtil.IsMissing(x)).ToList();

            // シーンは開くと現在のシーンを置換するため、一括では対象から外す。
            // シーン内オブジェクトも「選択」が一度に 1 つしか成立しないため除く
            var openable = alive
                .Where(x => !x.IsExternal && !x.IsSceneObject && !AssetStashUtil.IsFolder(x) && !AssetStashUtil.IsScene(x))
                .ToList();

            if (openable.Count > 0)
            {
                menu.AddItem(new GUIContent($"{openable.Count} 件を開く"), false, () =>
                {
                    foreach (var item in openable)
                    {
                        AssetStashUtil.OpenAsset(item);
                    }
                });
                menu.AddSeparator("");
            }

            var pingable = alive.Where(x => !x.IsExternal && !x.IsSceneObject).ToList();
            if (pingable.Count > 0)
            {
                menu.AddItem(new GUIContent($"{pingable.Count} 件の場所を示す"), false, () => AssetStashUtil.PingAssets(pingable));
            }

            var paths = alive.Select(AssetStashUtil.GetPath).Where(x => !string.IsNullOrEmpty(x)).ToList();
            var guids = selectedItems
                .Where(x => !x.IsGroup && !x.IsExternal && !string.IsNullOrEmpty(x.Guid))
                .Select(x => x.Guid)
                .ToList();

            if (paths.Count > 0 || guids.Count > 0)
            {
                menu.AddSeparator("");

                if (paths.Count > 0)
                {
                    menu.AddItem(new GUIContent($"{paths.Count} 件のパスをコピー"), false, () => AssetStashUtil.CopyToClipboard(string.Join("\n", paths)));
                }

                if (guids.Count > 0)
                {
                    menu.AddItem(new GUIContent($"{guids.Count} 件の GUID をコピー"), false, () => AssetStashUtil.CopyToClipboard(string.Join("\n", guids)));
                }
            }

            // グループだけを選択した場合はここまで項目が無く、先頭がセパレータになってしまう
            if (openable.Count > 0 || pingable.Count > 0 || paths.Count > 0 || guids.Count > 0)
            {
                menu.AddSeparator("");
            }

            menu.AddItem(new GUIContent($"選択中の {selectedItems.Count} 件を削除"), false, () => Delete(selectedItems));

            AddUndoMenuItem(menu);

            menu.DropDown(menuRect);
        }

        void AddUndoMenuItem(GenericMenu menu)
        {
            if (!undoHistory.CanUndo)
            {
                return;
            }

            var shortcut = Application.platform == RuntimePlatform.OSXEditor ? "Cmd+Z" : "Ctrl+Z";
            menu.AddSeparator("");
            menu.AddItem(new GUIContent($"元に戻す ({shortcut})"), false, PerformUndo);
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
                    Delete(stashTree.SelectedItems.ToList());
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

        #region ExportImport
        void OnExport()
        {
            var path = EditorUtility.SaveFilePanel(
                "AssetStash をエクスポート",
                "",
                $"AssetStash_{Application.productName}",
                "json");

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                File.WriteAllText(path, Bookmark.Serialize(isPathEnabled, isGUIDEnabled, isMemoVisible, assetsCache, true));
                ShowNotification(new GUIContent($"{assetsCache.Count} 件をエクスポートしました"));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("エクスポート", $"書き出しに失敗しました。\n\n{e.Message}", "OK");
            }
        }

        void OnImport()
        {
            var path = EditorUtility.OpenFilePanel("AssetStash をインポート", "", "json");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            AssetJson json;
            try
            {
                json = Bookmark.Deserialize(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("インポート", $"読み込みに失敗しました。\n\n{e.Message}", "OK");
                return;
            }

            if (json == null || json.Stash == null || json.Stash.Count == 0)
            {
                EditorUtility.DisplayDialog("インポート", "読み込める項目がありませんでした。", "OK");
                return;
            }

            if (json.Version > AssetJson.CurrentVersion)
            {
                EditorUtility.DisplayDialog(
                    "インポート",
                    $"このファイルは新しい形式です (version {json.Version})。\nAssetStash を更新してください。",
                    "OK");
                return;
            }

            var choice = EditorUtility.DisplayDialogComplex(
                "インポート",
                $"{json.Stash.Count} 件を読み込みます。\n\n" +
                "「追加」は今の内容を残したまま、重複を除いて取り込みます。\n" +
                "「置換」は今の内容をすべて破棄します。\n\n" +
                "どちらも Ctrl+Z で元に戻せます。",
                "追加",
                "キャンセル",
                "置換");

            if (choice == 1)
            {
                return;
            }

            var before = UndoHistory.CreateSnapshot(assetsCache);
            ClearSearch();

            if (choice == 0)
            {
                assetsCache = StashTransfer.Merge(assetsCache, json.Stash, ref CurrentID, out var added, out var skipped);
                ShowNotification(new GUIContent($"{added} 件を追加（重複 {skipped} 件をスキップ）"));
            }
            else
            {
                assetsCache = StashTransfer.Replace(json.Stash, ref CurrentID);
                ShowNotification(new GUIContent($"{assetsCache.Count} 件を読み込みました"));
            }

            undoHistory.Push(before);
            SaveStash(assetsCache);
            RebuildTree(assetsCache);
        }
        #endregion

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

            var before = UndoHistory.CreateSnapshot(assetsCache);

            ClearSearch();
            assetsCache.RemoveAll(AssetStashUtil.IsMissing);

            undoHistory.Push(before);
            SaveStash(assetsCache);
            RebuildTree(assetsCache);
        }
        #endregion

        #region Undo
        // グローバルの Edit/Undo より優先させるため、ウィンドウをコンテキストにした ShortcutManager で登録する
        [Shortcut("AssetStash/Undo", typeof(AssetStashWindow), KeyCode.Z, ShortcutModifiers.Action)]
        static void UndoShortcut(ShortcutArguments args) => (args.context as AssetStashWindow)?.PerformUndo();

        [Shortcut("AssetStash/Redo", typeof(AssetStashWindow), KeyCode.Y, ShortcutModifiers.Action)]
        static void RedoShortcut(ShortcutArguments args) => (args.context as AssetStashWindow)?.PerformRedo();

        [Shortcut("AssetStash/Redo Alt", typeof(AssetStashWindow), KeyCode.Z, ShortcutModifiers.Action | ShortcutModifiers.Shift)]
        static void RedoAltShortcut(ShortcutArguments args) => (args.context as AssetStashWindow)?.PerformRedo();

        void PerformUndo()
        {
            ApplyHistory(undoHistory.Undo(assetsCache));
        }

        void PerformRedo()
        {
            ApplyHistory(undoHistory.Redo(assetsCache));
        }

        void ApplyHistory(List<AssetData> restored)
        {
            if (restored == null)
            {
                return;
            }

            assetsCache = restored;

            // ID を再利用すると復元済みの項目と衝突するため、採番は戻さない
            if (assetsCache.Count > 0)
            {
                CurrentID = Mathf.Max(CurrentID, assetsCache.Max(x => x.ID) + 1);
            }

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

            // シーン内オブジェクトは GUID 列に GlobalObjectId を出しているので、そちらも対象にする
            var id = item.IsSceneObject ? item.GlobalId : item.Guid;

            foreach (var keyword in keywords)
            {
                if (!Contains(name, keyword)
                    && !Contains(path, keyword)
                    && !Contains(id, keyword)
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
            Delete(new[] { item });
        }

        public void Delete(IReadOnlyList<AssetData> items)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            var targetIds = new HashSet<int>(items.Select(x => x.ID));

            var removeCount = assetsCache.Count(a => targetIds.Contains(a.ID) || targetIds.Contains(a.ParentID));

            if (!ConfirmDelete(items, removeCount))
            {
                return;
            }

            var before = UndoHistory.CreateSnapshot(assetsCache);

            assetsCache.RemoveAll(a => targetIds.Contains(a.ID) || targetIds.Contains(a.ParentID));

            undoHistory.Push(before);
            SaveStash(assetsCache);
            RebuildTree(assetsCache);
        }

        bool ConfirmDelete(IReadOnlyList<AssetData> items, int removeCount)
        {
            if (removeCount <= 1)
            {
                return true;
            }

            var message = items.Count == 1
                ? $"グループ「{items[0].Name}」と、配下の {removeCount - 1} 件のブックマークを削除します。"
                : $"選択中の {items.Count} 件（配下を含めて {removeCount} 件）を削除します。";

            return EditorUtility.DisplayDialog(
                "削除",
                $"{message}\n\nこの操作は Ctrl+Z で元に戻せます。",
                "削除",
                "キャンセル");
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

            foreach (var m in CollectDraggedItems(draggedIds))
            {
                if (m.IsGroup || AssetStashUtil.IsMissing(m))
                {
                    continue;
                }

                // シーン内オブジェクトはシーンアセットではなく、解決した GameObject を渡す
                if (m.IsSceneObject)
                {
                    var sceneObject = AssetStashUtil.ResolveSceneObject(m);
                    if (sceneObject != null)
                    {
                        objectRefs.Add(sceneObject);
                    }
                    continue;
                }

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

            var startArgs = new StartDragArgs("Dragging Assets", DragVisualMode.Copy);

            // ここで渡した値だけがドラッグ開始時の PrepareStartDrag を生き延びる。
            // ドラッグ単位で保持されるため、中断した情報が次のドラッグに残らない
            startArgs.SetGenericData(DraggedIdsKey, draggedIds);
            startArgs.SetGenericData(DraggedObjectsKey, objectRefs.ToArray());
            startArgs.SetGenericData(DraggedPathsKey, paths.ToArray());

            return startArgs;
        }

        static int[] GetDraggedIds() => DragAndDrop.GetGenericData(DraggedIdsKey) as int[];

        // ドラッグ中に objectReferences が落ちることがあるため、開始時の内容から復元する
        static void RestoreDragData()
        {
            if (DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0)
            {
                return;
            }

            var objects = DragAndDrop.GetGenericData(DraggedObjectsKey) as UnityEngine.Object[];
            if (objects == null || objects.Length == 0)
            {
                return;
            }

            DragAndDrop.objectReferences = objects;
            DragAndDrop.paths = DragAndDrop.GetGenericData(DraggedPathsKey) as string[] ?? new string[0];
        }

        DragVisualMode DragAndDropUpdate(HandleDragAndDropArgs args, int[] draggedIds)
        {
            if (IsFiltering)
            {
                return DragVisualMode.Rejected;
            }

            RestoreDragData();

            var dragged = draggedIds;
            bool hasExternal = DragAndDrop.paths != null && DragAndDrop.paths.Length > 0;
            bool hasObjectRefs = DragAndDrop.objectReferences != null && DragAndDrop.objectReferences.Length > 0;

            if ((dragged == null || dragged.Length == 0) && !hasExternal && !hasObjectRefs)
            {
                return DragVisualMode.Rejected;
            }

            int targetParent = args.parentId;

            if (dragged != null && dragged.Length > 0)
            {
                if (dragged.Any(id => id == targetParent))
                {
                    return DragVisualMode.Rejected;
                }

                if (targetParent != -1 && CollectDraggedItems(dragged).Any(x => x.IsGroup))
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

            RestoreDragData();

            var dragged = draggedIds;
            bool hasExternalDrop = DragAndDrop.paths != null && DragAndDrop.paths.Length > 0;
            var sceneObjects = GetDroppedSceneObjects();

            if ((dragged == null || dragged.Length == 0) && !hasExternalDrop && sceneObjects.Length == 0)
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
            else
            {
                OnDrop(args, sceneObjects);
            }

            DragAndDrop.AcceptDrag();

            return DragVisualMode.Move;
        }

        // Hierarchy からのドラッグは paths を持たず objectReferences だけが入る
        static GameObject[] GetDroppedSceneObjects()
        {
            if (DragAndDrop.objectReferences == null)
            {
                return new GameObject[0];
            }

            return DragAndDrop.objectReferences
                .OfType<GameObject>()
                .Where(Bookmark.CanBookmarkSceneObject)
                .ToArray();
        }

        void OnDrop(HandleDragAndDropArgs args, GameObject[] sceneObjects)
        {
            if (sceneObjects.Length == 0)
            {
                return;
            }

            var before = UndoHistory.CreateSnapshot(assetsCache);
            var targetItem = stashTree.GetItemDataForIndex(args.insertAtIndex);
            var added = new List<AssetData>();

            foreach (var go in sceneObjects)
            {
                var info = Bookmark.CreateFromSceneObject(go, CurrentID + 1);
                if (assetsCache.Any(x => x.IsSceneObject && x.GlobalId == info.GlobalId))
                {
                    continue;
                }

                CurrentID++;
                added.Add(info);
            }

            if (added.Count == 0)
            {
                return;
            }

            var parent = args.target as AssetData;
            var parentId = parent == null ? args.parentId
                : parent.IsGroup ? parent.ID
                : parent.ParentID;

            foreach (var info in added)
            {
                info.ParentID = parentId;
            }

            var anchor = parent != null && parent.IsGroup ? null : targetItem;
            var insertIndex = anchor == null ? assetsCache.Count : assetsCache.FindIndex(x => x.ID == anchor.ID);
            if (insertIndex < 0)
            {
                insertIndex = assetsCache.Count;
            }

            assetsCache.InsertRange(insertIndex, added);

            undoHistory.Push(before);
            SaveStash(assetsCache);
            RebuildTree(assetsCache);

            stashTree.SetSelectionByIds(added.Select(x => x.ID).ToList());
        }

        void OnDrop(HandleDragAndDropArgs args, int[] dragged)
        {
            var items = CollectDraggedItems(dragged);
            if (items.Count == 0)
            {
                return;
            }

            var target = args.target as AssetData;
            var droppedOnGroup = target != null && target.IsGroup;

            var parentId = droppedOnGroup ? target.ID
                : target != null ? target.ParentID
                : args.parentId;

            var anchor = droppedOnGroup ? null : stashTree.GetItemDataForIndex(args.insertAtIndex);

            if (items.Any(x => x.ID == parentId))
            {
                return;
            }

            var before = UndoHistory.CreateSnapshot(assetsCache);

            var block = BuildMoveBlock(items, parentId);
            var blockIds = new HashSet<int>(block.Select(x => x.ID));

            if (anchor != null && blockIds.Contains(anchor.ID))
            {
                anchor = null;
            }

            assetsCache.RemoveAll(x => blockIds.Contains(x.ID));

            var insertIndex = anchor == null ? assetsCache.Count : assetsCache.FindIndex(x => x.ID == anchor.ID);
            if (insertIndex < 0)
            {
                insertIndex = assetsCache.Count;
            }

            assetsCache.InsertRange(insertIndex, block);

            undoHistory.Push(before);
            SaveStash(assetsCache);
            RebuildTree(assetsCache);

            stashTree.SetSelectionByIds(items.Select(x => x.ID).ToList());
        }

        List<AssetData> CollectDraggedItems(int[] dragged)
        {
            if (dragged == null || dragged.Length == 0 || assetsCache == null)
            {
                return new List<AssetData>();
            }

            var draggedIds = new HashSet<int>(dragged);
            return assetsCache.Where(x => draggedIds.Contains(x.ID)).ToList();
        }

        List<AssetData> BuildMoveBlock(List<AssetData> items, int parentId)
        {
            var draggedIds = new HashSet<int>(items.Select(x => x.ID));
            var added = new HashSet<int>();
            var block = new List<AssetData>();

            foreach (var item in items)
            {
                if (item.ParentID != -1 && draggedIds.Contains(item.ParentID))
                {
                    continue;
                }

                if (!added.Add(item.ID))
                {
                    continue;
                }

                item.ParentID = item.IsGroup ? -1 : parentId;
                block.Add(item);

                if (!item.IsGroup)
                {
                    continue;
                }

                foreach (var child in assetsCache.Where(x => x.ParentID == item.ID).ToList())
                {
                    if (added.Add(child.ID))
                    {
                        block.Add(child);
                    }
                }
            }

            return block;
        }

        void OnDrop(HandleDragAndDropArgs args, string[] draggedPath)
        {
            AssetData refreshItem = null;
            var before = UndoHistory.CreateSnapshot(assetsCache);

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

            if (assetsCache.Count != before.Count)
            {
                undoHistory.Push(before);
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