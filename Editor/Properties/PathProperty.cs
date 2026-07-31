using UnityEngine.UIElements;

namespace KuonLib.AssetStash.Properties
{
    public class PathProperty : StashProperty
    {
        public PathProperty(AssetStashTree tree) : base(tree) { }

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
                e.Q<Label>().text = item.IsExternal ? item.Name : AssetStashUtil.GuidToPath(item.Guid);
            };
        }
    }
}
