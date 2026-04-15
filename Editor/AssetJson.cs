using System;
using System.Collections.Generic;

namespace KuonLib.AssetStash
{
    [Serializable]
    public class AssetJson
    {
        public bool IsPathEnabled;
        public bool IsGUIDEnabled;
        public bool IsMemoEnabled;
        public List<AssetData> Stash;
    }
}