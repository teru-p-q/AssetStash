using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KuonLib.AssetStash
{
    public partial class AssetStashWindow : IHasCustomMenu
    {
        // ツールバーを増やさずに済むよう、エクスポート / インポートはウィンドウのメニューに置く
        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("エクスポート..."), false, OnExport);
            menu.AddItem(new GUIContent("インポート..."), false, OnImport);
        }

        [MenuItem("Window/AssetStashWindow")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<AssetStashWindow>();
            wnd.titleContent = new GUIContent("AssetStashWindow");
            wnd.Reload();
        }

        private void OnEnable()
        {
            // シーンの開閉でシーン内オブジェクトの解決結果が変わる
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
        }

        public void OnDisable()
        {
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
        }

        bool sceneRefreshQueued;

        void OnSceneOpened(Scene scene, OpenSceneMode mode) => ScheduleSceneObjectRefresh();

        void OnSceneClosed(Scene scene) => ScheduleSceneObjectRefresh();

        // シーンの開閉の途中でオブジェクトを解決しようとすると内部アサーションが出るため、
        // 遷移が終わってからまとめて更新する
        void ScheduleSceneObjectRefresh()
        {
            if (sceneRefreshQueued)
            {
                return;
            }

            sceneRefreshQueued = true;

            EditorApplication.delayCall += () =>
            {
                sceneRefreshQueued = false;

                if (this == null)
                {
                    return;
                }

                RefreshSceneObjects();
            };
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