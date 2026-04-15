using UnityEngine.UIElements;

namespace KuonLib.AssetStash.Properties
{
    public class GUIDProperty : StashProperty
    {
        readonly AssetStashTree stashTree;

        public GUIDProperty(AssetStashTree tree)
        {
            stashTree = tree;
        }

        public override void Create(Column column, Toggle toggle, bool isVisible)
        {
            this.column = column;
            this.toggle = toggle;
            IsVisible = isVisible;

            column.bindCell = (e, i) =>
            {
                var item = stashTree.GetItemDataForIndex(i);
                if (item == null)
                {
                    return;
                }
                e.Q<Label>().text = item.Guid.ToString();
            };

            toggle.RegisterValueChangedCallback(evt =>
            {
                IsVisible = evt.newValue;
            });

            AssetStashUtil.SetDefaultToggleStyle(toggle);
        }
    }
}
