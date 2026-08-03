using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
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

            if (data.IsSceneObject)
            {
                return IsSceneObjectMissing(data);
            }

            // GUIDToAssetPath は削除後も同一セッション中は古いパスを返し続けるため、実在も確認する
            var path = GuidToPath(data.Guid);
            return string.IsNullOrEmpty(path) || !AssetDatabase.AssetPathExists(path);
        }

        static bool IsSceneObjectMissing(AssetData data)
        {
            var scenePath = GuidToPath(data.Guid);
            if (string.IsNullOrEmpty(scenePath) || !AssetDatabase.AssetPathExists(scenePath))
            {
                return true;
            }

            // シーンが開かれていなければオブジェクトの生死は判定できないので、欠損とはしない
            if (!IsSceneLoaded(scenePath))
            {
                return false;
            }

            return ResolveSceneObject(data) == null;
        }

        public static bool IsSceneLoaded(string scenePath)
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.path == scenePath)
                {
                    return true;
                }
            }

            return false;
        }

        // GlobalObjectIdentifierToObjectSlow は名前のとおり重いので、
        // ヒエラルキーが変わるまで結果を使い回す
        static readonly Dictionary<string, UnityEngine.Object> sceneObjectCache = new();
        static bool sceneObjectCacheHooked;

        public static void ClearSceneObjectCache() => sceneObjectCache.Clear();

        // 解決できなかった結果も記憶するため、シーンの開閉でも必ず捨てる。
        // これを怠ると、シーンを開き直しても欠損のまま表示され続ける
        static void EnsureSceneObjectCacheHooked()
        {
            if (sceneObjectCacheHooked)
            {
                return;
            }

            sceneObjectCacheHooked = true;
            EditorApplication.hierarchyChanged += ClearSceneObjectCache;
            EditorSceneManager.sceneOpened += (scene, mode) => ClearSceneObjectCache();
            EditorSceneManager.sceneClosed += scene => ClearSceneObjectCache();
        }

        public static UnityEngine.Object ResolveSceneObject(AssetData data)
        {
            if (data == null || !data.IsSceneObject || string.IsNullOrEmpty(data.GlobalId))
            {
                return null;
            }

            EnsureSceneObjectCacheHooked();

            if (sceneObjectCache.TryGetValue(data.GlobalId, out var cached))
            {
                return cached;
            }

            // シーンが読み込まれていない状態で解決を試みると
            // GlobalObjectIdentifierToObjectSlow が内部アサーションを出すため、
            // 読み込み済みのときだけ問い合わせる（未読み込みなら解決できないので結果も同じ）
            var scenePath = GuidToPath(data.Guid);
            if (string.IsNullOrEmpty(scenePath) || !IsSceneLoaded(scenePath))
            {
                return null;
            }

            UnityEngine.Object resolved = null;
            if (GlobalObjectId.TryParse(data.GlobalId, out var globalObjectId))
            {
                resolved = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId);
            }

            sceneObjectCache[data.GlobalId] = resolved;
            return resolved;
        }

        public static void SelectSceneObject(AssetData data)
        {
            var obj = ResolveSceneObject(data);

            if (obj == null)
            {
                // シーンが開かれていない場合は開いてから選択する
                var scenePath = GuidToPath(data.Guid);
                if (string.IsNullOrEmpty(scenePath) || !AssetDatabase.AssetPathExists(scenePath))
                {
                    return;
                }

                if (IsSceneLoaded(scenePath) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    return;
                }

                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                sceneObjectCache.Clear();
                obj = ResolveSceneObject(data);
            }

            if (obj == null)
            {
                return;
            }

            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }

        public static string GetMissingTooltip(AssetData data)
        {
            if (data.IsExternal)
            {
                return $"ファイルが見つかりません: {data.Name}";
            }

            if (data.IsSceneObject)
            {
                var scenePath = GuidToPath(data.Guid);
                return string.IsNullOrEmpty(scenePath) || !AssetDatabase.AssetPathExists(scenePath)
                    ? "シーンが見つかりません"
                    : $"シーン内にオブジェクトが見つかりません ({scenePath})";
            }

            return $"アセットが見つかりません (GUID: {data.Guid})";
        }

        public static void OpenAsset(AssetData data)
        {
            if (IsMissing(data))
            {
                return;
            }

            if (data.IsSceneObject)
            {
                SelectSceneObject(data);
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

        public static void OpenSceneAdditive(AssetData data)
        {
            if (IsMissing(data) || !IsScene(data))
            {
                return;
            }

            EditorSceneManager.OpenScene(GuidToPath(data.Guid), OpenSceneMode.Additive);
        }

        public static string GuidToPath(string guid)
        {
            return AssetDatabase.GUIDToAssetPath(guid);
        }

        // External はパスを Name に保持している
        public static string GetPath(AssetData data)
        {
            if (data == null || data.IsGroup)
            {
                return "";
            }

            return data.IsExternal ? data.Name : GuidToPath(data.Guid);
        }

        public static bool IsFolder(AssetData data)
        {
            var path = GetPath(data);
            return !string.IsNullOrEmpty(path) && (AssetDatabase.IsValidFolder(path) || Directory.Exists(path));
        }

        public static void CopyToClipboard(string text)
        {
            EditorGUIUtility.systemCopyBuffer = text ?? "";
        }

        // SceneObject も Guid はシーンを指すが、シーンアセットそのものではない
        public static bool IsScene(AssetData data) => !data.IsSceneObject && Path.GetExtension(GuidToPath(data.Guid)).Equals(".unity");

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
                .Where(x => !IsMissing(x) && !x.IsSceneObject && !string.IsNullOrEmpty(x.Guid))
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
            var path = GetPath(data);
            if (IsMissing(data) || string.IsNullOrEmpty(path))
            {
                return;
            }

            EditorUtility.RevealInFinder(path);
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
