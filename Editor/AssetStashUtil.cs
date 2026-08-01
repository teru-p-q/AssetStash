using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;

namespace KuonLib.AssetStash
{
    public class AssetStashUtil
    {
        public static bool IsMissing(AssetData data)
        {
            if (data == null || data.IsGroup)
            {
                return false;
            }

            if (data.IsExternal)
            {
                return string.IsNullOrEmpty(data.Name) || (!File.Exists(data.Name) && !Directory.Exists(data.Name));
            }

            return string.IsNullOrEmpty(GuidToPath(data.Guid));
        }

        public static string GetMissingTooltip(AssetData data)
        {
            return data.IsExternal
                ? $"ファイルが見つかりません: {data.Name}"
                : $"アセットが見つかりません (GUID: {data.Guid})";
        }

        public static void OpenAsset(AssetData data)
        {
            if (IsMissing(data))
            {
                return;
            }

            if (IsScene(data))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(GuidToPath(data.Guid), OpenSceneMode.Single);
                }
            }
            else
            {
                var path = GuidToPath(data.Guid);
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                AssetDatabase.OpenAsset(asset);
            }
        }

        public static string GuidToPath(string guid)
        {
            return AssetDatabase.GUIDToAssetPath(guid);
        }

        public static bool IsScene(AssetData data) => Path.GetExtension(GuidToPath(data.Guid)).Equals(".unity");

        public static void PingAsset(AssetData data)
        {
            if (!string.IsNullOrEmpty(data.Guid))
            {
                var path = AssetStashUtil.GuidToPath(data.Guid);
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (obj != null)
                {
                    Selection.activeObject = obj;
                    EditorGUIUtility.PingObject(obj);
                }
            }
        }

        public static void PingAssets(IReadOnlyList<AssetData> items)
        {
            var objects = items
                .Where(x => !IsMissing(x) && !string.IsNullOrEmpty(x.Guid))
                .Select(x => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(GuidToPath(x.Guid)))
                .Where(x => x != null)
                .ToArray();

            if (objects.Length == 0)
            {
                return;
            }

            Selection.objects = objects;
            EditorGUIUtility.PingObject(objects[0]);
        }

        public static void OpenFolder(AssetData data)
        {
            EditorUtility.RevealInFinder(data.Name);
        }

        public static void SetDefaultToggleStyle(Toggle toggle)
        {
            toggle.labelElement.style.minWidth = 0;
            toggle.labelElement.style.width = StyleKeyword.Auto;
            toggle.labelElement.style.flexBasis = StyleKeyword.Auto;
            toggle.labelElement.style.marginLeft = 5;
            toggle.labelElement.style.marginRight = 5;
        }
    }
}
