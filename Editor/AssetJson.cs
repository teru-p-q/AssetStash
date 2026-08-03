using System;
using System.Collections.Generic;

namespace KuonLib.AssetStash
{
    [Serializable]
    public class AssetJson
    {
        // 書き出したファイルは外部に渡るため、読み込み側が世代を判別できるようにする
        public const int CurrentVersion = 1;

        public int Version;
        public bool IsPathEnabled;
        public bool IsGUIDEnabled;
        public bool IsMemoEnabled;
        public List<AssetData> Stash;
    }
}