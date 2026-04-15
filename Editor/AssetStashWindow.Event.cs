using UnityEditor;
using UnityEngine;

namespace KuonLib.AssetStash
{
    public partial class AssetStashWindow
    {

        [MenuItem("Window/AssetStashWindow")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<AssetStashWindow>();
            wnd.titleContent = new GUIContent("AssetStashWindow");
            wnd.Reload();
        }

        private void OnEnable()
        {
        }

        public void OnDisable()
        {
        }

        void Reset()
        {
            if (assetsCache == null)
            {
                assetsCache = new();
            }
            assetsCache.Clear();
            RebuildTree(assetsCache);
        }

        public void CreateGUI()
        {
            CreateBookmarkGUI();
        }
    }
}