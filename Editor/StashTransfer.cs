using System.Collections.Generic;
using System.Linq;

namespace KuonLib.AssetStash
{
    /// <summary>
    /// エクスポートしたファイルを取り込む際の、ID 振り直しと重複判定
    /// </summary>
    public static class StashTransfer
    {
        // 取り込んだ ID は既存と衝突するため必ず振り直す。並び順は元のまま保つ
        public static List<AssetData> Replace(List<AssetData> imported, ref int nextId)
        {
            var idMap = new Dictionary<int, int>();
            var result = new List<AssetData>();

            foreach (var item in imported)
            {
                var clone = (AssetData)item.Clone();
                clone.ID = ++nextId;
                idMap[item.ID] = clone.ID;
                result.Add(clone);
            }

            foreach (var clone in result)
            {
                clone.ParentID = MapParent(clone.ParentID, idMap);
            }

            return result;
        }

        public static List<AssetData> Merge(List<AssetData> current, List<AssetData> imported, ref int nextId, out int added, out int skipped)
        {
            var result = new List<AssetData>(current);
            var idMap = new Dictionary<int, int>();

            added = 0;
            skipped = 0;

            // imported はグループが子より先に並んでいるため、親の対応付けは 1 パスで足りる
            foreach (var item in imported)
            {
                if (item.IsGroup)
                {
                    // 同名のグループがあれば再利用する（同じファイルを 2 回入れても増えない）
                    var existing = result.FirstOrDefault(x => x.IsGroup && x.Name == item.Name);
                    if (existing != null)
                    {
                        idMap[item.ID] = existing.ID;
                        skipped++;
                        continue;
                    }

                    var group = (AssetData)item.Clone();
                    group.ID = ++nextId;
                    group.ParentID = -1;
                    idMap[item.ID] = group.ID;

                    result.Add(group);
                    added++;
                    continue;
                }

                if (result.Any(x => IsSameEntry(x, item)))
                {
                    skipped++;
                    continue;
                }

                var clone = (AssetData)item.Clone();
                clone.ID = ++nextId;
                clone.ParentID = MapParent(item.ParentID, idMap);

                result.Add(clone);
                added++;
            }

            return result;
        }

        static int MapParent(int parentId, Dictionary<int, int> idMap)
        {
            if (parentId != -1 && idMap.TryGetValue(parentId, out var mapped))
            {
                return mapped;
            }

            return -1;
        }

        // 同じ対象を指しているかは種別ごとに判定材料が違う
        static bool IsSameEntry(AssetData a, AssetData b)
        {
            if (a.IsGroup || b.IsGroup)
            {
                return false;
            }

            if (a.IsSceneObject || b.IsSceneObject)
            {
                return a.IsSceneObject && b.IsSceneObject
                    && !string.IsNullOrEmpty(a.GlobalId) && a.GlobalId == b.GlobalId;
            }

            if (a.IsExternal || b.IsExternal)
            {
                return a.IsExternal && b.IsExternal
                    && !string.IsNullOrEmpty(a.Name) && a.Name == b.Name;
            }

            return !string.IsNullOrEmpty(a.Guid) && a.Guid == b.Guid;
        }
    }
}
