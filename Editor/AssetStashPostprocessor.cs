using UnityEditor;

namespace KuonLib.AssetStash
{
    /// <summary>
    /// アセットのリネーム / 移動 / 削除に追従して、開いているウィンドウの表示を更新する
    /// </summary>
    public class AssetStashPostprocessor : AssetPostprocessor
    {
        static bool refreshQueued;

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (importedAssets.Length == 0 && deletedAssets.Length == 0 && movedAssets.Length == 0)
            {
                return;
            }

            if (refreshQueued)
            {
                return;
            }

            refreshQueued = true;

            EditorApplication.delayCall += () =>
            {
                refreshQueued = false;
                AssetStashWindow.RefreshOpenWindows();
            };
        }
    }
}
