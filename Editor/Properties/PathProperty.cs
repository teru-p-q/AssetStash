using Unity.VisualScripting.YamlDotNet.Core;
using UnityEngine.UIElements;

namespace KuonLib.AssetStash.Properties
{
    public class PathProperty : StashProperty
    {
        AssetStashTree stashTree;

        public PathProperty(AssetStashTree tree)
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
                if (item.Type == "external")
                {
                    e.Q<Label>().text = item.Name;
                }
                else
                {
                    e.Q<Label>().text = AssetStashUtil.GuidToPath(item.Guid);
                }
            };

            toggle.RegisterValueChangedCallback(evt =>
            {
                IsVisible = evt.newValue;
            });
            AssetStashUtil.SetDefaultToggleStyle(toggle);
        }
    }
}
