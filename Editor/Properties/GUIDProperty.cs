using UnityEngine.UIElements;

namespace KuonLib.AssetStash.Properties
{
    public class GUIDProperty : StashProperty
    {
        public GUIDProperty(AssetStashTree tree) : base(tree) { }

        public override void Create(Column column, Toggle toggle, bool isVisible)
        {
            SetupColumn(column, toggle, isVisible);

            column.bindCell = (e, i) =>
            {
                var item = stashTree.GetItemDataForIndex(i);
                if (item == null)
                {
                    return;
                }
                // シーン内オブジェクトはシーンの GUID より GlobalObjectId のほうが識別に役立つ
                e.Q<Label>().text = item.IsSceneObject ? item.GlobalId : item.Guid;
            };
        }
    }
}
