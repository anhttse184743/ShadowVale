using System;
using System.Collections.Generic;
using UnityEngine;
using Cell = ShadowVale.Map01.Map01Inventory.BagCell;

namespace ShadowVale.Map01
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ForestPoint))]
    public sealed class Map01StorageChest : MonoBehaviour
    {
        public const int Columns = 8, Rows = 8, Capacity = Columns * Rows;
        public const string DisplayName = "Rương quân nhu";
        [SerializeField] private Transform lid;
        private readonly Cell[] cells = new Cell[Capacity];
        private readonly Queue<Cell> overflow = new Queue<Cell>();
        private Quaternion closedRotation;
        private Vector3 hingeAxis;
        private float lidProgress;
        private bool isOpen;

        [Serializable]
        public struct SavedStack
        {
            public int index, count;
            public string id;
            public float condition;
        }

        public Cell CellAt(int index) => index >= 0 && index < Capacity ? cells[index] : null;
        public int Occupied { get; private set; }
        public int OverflowCount => overflow.Count;
        internal bool LidIsOpen => lid == null || lidProgress >= 1f;

        internal void Put(int index, Cell cell)
        {
            // Stacks from the old 32 × 32 chest fill freed slots in their saved order.
            if (cell == null && cells[index] != null && overflow.Count > 0)
                cell = overflow.Dequeue();
            if (cells[index] == null && cell != null) Occupied++;
            else if (cells[index] != null && cell == null) Occupied--;
            cells[index] = cell;
        }

        internal SavedStack[] Capture()
        {
            var saved = new List<SavedStack>(Occupied + overflow.Count);
            for (int i = 0; i < Capacity; i++)
            {
                var cell = cells[i];
                if (cell != null)
                    saved.Add(Snapshot(i, cell));
            }
            int overflowIndex = Capacity;
            foreach (var cell in overflow) saved.Add(Snapshot(overflowIndex++, cell));
            return saved.ToArray();
        }

        private static SavedStack Snapshot(int index, Cell cell) =>
            new SavedStack { index = index, id = cell.id, count = cell.count, condition = cell.condition };

        internal void Restore(SavedStack[] saved)
        {
            Array.Clear(cells, 0, Capacity);
            overflow.Clear();
            Occupied = 0;
            if (saved == null) return;
            foreach (var stack in saved)
            {
                if (stack.index < 0 || stack.count <= 0 || string.IsNullOrEmpty(stack.id)) continue;
                var cell = new Cell { id = stack.id, count = stack.count, condition = stack.condition };
                if (stack.index < Capacity) Put(stack.index, cell);
                else overflow.Enqueue(cell);
            }
            for (int i = 0; i < Capacity && overflow.Count > 0; i++)
                if (cells[i] == null) Put(i, overflow.Dequeue());
        }

        internal void SetOpen(bool open)
        {
            if (isOpen == open) return;
            isOpen = open;
            if (open) Map01OperationAudio.Play("chest_open", transform.position + Vector3.up * .35f);
        }

        private void Awake()
        {
            if (lid == null) return;
            closedRotation = lid.localRotation;
            hingeAxis = lid.parent.InverseTransformDirection(transform.right);
        }

        private void Update()
        {
            if (lid == null) return;
            float next = Mathf.MoveTowards(lidProgress, isOpen ? 1f : 0f, Time.deltaTime / .48f);
            if (next == lidProgress) return;
            lidProgress = next;
            lid.localRotation = Quaternion.AngleAxis(-76f * Mathf.SmoothStep(0, 1, next), hingeAxis) * closedRotation;
        }
    }
}
