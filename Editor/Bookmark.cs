using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.UIElements;

namespace KuonLib.AssetStash
{
    public class Bookmark
    {
        static string PrefsKey => $"{Application.productName}-AssetStash";

        public static (bool isPathEnabled, bool isGUIDEnabled, bool isMemoVisible, List<AssetData> stash) LoadPrefs()
        {
            List<TreeViewItemData<AssetData>> stash = new();

            string prefsKey = PrefsKey;
            if (!EditorPrefs.HasKey(prefsKey))
            {
                return new(false, false, false, new());
            }

            string prefsJson = EditorPrefs.GetString(prefsKey);
            var assetsJson = JsonUtility.FromJson<AssetJson>(prefsJson);
            if (assetsJson == null)
            {
                return new(false, false, false, new());
            }

            // Name は GUID から取得しなおす
            foreach (var assetData in assetsJson.Stash)
            {
                if (assetData.Guid != null)
                {
                    var path = AssetDatabase.GUIDToAssetPath(assetData.Guid);
                    var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                    if (asset != null)
                    {
                        assetData.Name = asset.name;
                    }
                }
            }

            return (assetsJson.IsPathEnabled, assetsJson.IsGUIDEnabled, assetsJson.IsMemoEnabled, assetsJson.Stash);
        }

        public static void ResetPrefs()
        {
            EditorPrefs.SetString(PrefsKey, "");
        }

        public static void SavePrefs(bool isPathEnabled, bool isGUIDEnabled, bool isMemoEnabled, List<AssetData> items)
        {
            var prefsJson = JsonUtility.ToJson(
                new AssetJson {
                    IsPathEnabled = isPathEnabled,
                    IsGUIDEnabled = isGUIDEnabled,
                    IsMemoEnabled = isMemoEnabled,
                    Stash = items,
                });
            EditorPrefs.SetString(PrefsKey, prefsJson);
        }

        public static AssetData CreateFromPath(string path, int newID)
        {
            var assetGuid = AssetDatabase.AssetPathToGUID(path);
            if (assetGuid == "")
            {
                return new AssetData()
                {
                    Guid = assetGuid,
                    ID = newID,
                    Name = path,
                    Memo = "",
                    Type = "external",
                    ParentID = -1,
                    IsExpanded = false,
                };
            }
            else
            {
                var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                return new AssetData()
                {
                    Guid = assetGuid,
                    ID = newID,
                    Name = asset.name,
                    Memo = "",
                    Type = asset.GetType().ToString(),
                    ParentID = -1,
                    IsExpanded = false,
                };
            }
        }

        public static AssetData Add(List<AssetData> items, string assetGuid, AssetData parentAsset, int newID)
        {
            var path = AssetDatabase.GUIDToAssetPath(assetGuid);
            var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            var info = new AssetData()
            {
                Guid = assetGuid,
                ID = newID,
                Name = asset.name,
                Memo = "",
                Type = asset.GetType().ToString(),
                ParentID = -1,
                IsExpanded = false,
            };

            if (parentAsset != null)
            {
                if (parentAsset.IsGroup)
                {
                    info.ParentID = parentAsset.ID;
                }
                else
                {
                    info.ParentID = parentAsset.ParentID;
                }
                items.Insert(items.FindIndex(x => x.ID == parentAsset.ID) + 1, info);
            }
            else
            {
                items.Add(info);
            }

            return info;
        }
    }
}
