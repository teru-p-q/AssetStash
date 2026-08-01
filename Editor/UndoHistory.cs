using System.Collections.Generic;
using System.Linq;

namespace KuonLib.AssetStash
{
    public class UndoHistory
    {
        const int Capacity = 50;

        readonly LinkedList<List<AssetData>> undoStack = new();
        readonly Stack<List<AssetData>> redoStack = new();

        public bool CanUndo => undoStack.Count > 0;
        public bool CanRedo => redoStack.Count > 0;

        public static List<AssetData> CreateSnapshot(List<AssetData> items)
        {
            return items == null ? new List<AssetData>() : items.Select(x => (AssetData)x.Clone()).ToList();
        }

        // 変更前に CreateSnapshot で取得した状態を、変更が確定したタイミングで積む
        public void Push(List<AssetData> snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            undoStack.AddLast(snapshot);
            if (undoStack.Count > Capacity)
            {
                undoStack.RemoveFirst();
            }

            redoStack.Clear();
        }

        public List<AssetData> Undo(List<AssetData> current)
        {
            if (!CanUndo)
            {
                return null;
            }

            redoStack.Push(CreateSnapshot(current));

            var restored = undoStack.Last.Value;
            undoStack.RemoveLast();
            return restored;
        }

        public List<AssetData> Redo(List<AssetData> current)
        {
            if (!CanRedo)
            {
                return null;
            }

            undoStack.AddLast(CreateSnapshot(current));
            return redoStack.Pop();
        }

        public void Clear()
        {
            undoStack.Clear();
            redoStack.Clear();
        }
    }
}
