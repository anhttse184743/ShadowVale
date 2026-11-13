using UnityEngine;

namespace ShadowVale.Map01
{
    public sealed partial class Map01Inventory
    {
        internal int TransferStorage(Map01StorageChest chest, bool fromChest, int from, int to = -1)
        {
            var source = fromChest ? chest.CellAt(from) : Cell(from);
            if (source == null || IsMissionItem(source.id) || source.id == "knife") return 0;

            int amount = fromChest && source.id == "stone" ? Mathf.Min(StoneRoom, source.count) : source.count;
            int original = amount;
            int capacity = fromChest ? BagSize : Map01StorageChest.Capacity;
            int stackLimit = StackLimit(source.id);
            if (to >= 0 && to < capacity) MoveTo(to);
            else if (to == -1)
            {
                for (int i = 0; i < capacity && amount > 0; i++)
                    if (Destination(i)?.id == source.id) MoveTo(i);
                for (int i = 0; i < capacity && amount > 0; i++)
                    if (Destination(i) == null) MoveTo(i);
            }

            if (amount == original) return 0;
            if (source.count == 0)
            {
                if (fromChest) chest.Put(from, null);
                else bag[from] = null;
            }
            Changed?.Invoke();
            return original - amount;

            BagCell Destination(int index) => fromChest ? bag[index] : chest.CellAt(index);

            void MoveTo(int index)
            {
                var target = Destination(index);
                if (target != null && target.id != source.id) return;
                int moved = Mathf.Min(amount, stackLimit - (target?.count ?? 0));
                if (moved <= 0) return;
                if (target != null) target.count += moved;
                else
                {
                    var stack = new BagCell { id = source.id, count = moved, condition = source.condition };
                    if (fromChest) bag[index] = stack;
                    else chest.Put(index, stack);
                }
                amount -= moved;
                source.count -= moved;
            }
        }
    }
}
