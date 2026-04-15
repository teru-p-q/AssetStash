using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;

namespace KuonLib.AssetStash
{
    public class AssetStashUtil
    {
        public static void OpenAsset(AssetData data)
        {
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
