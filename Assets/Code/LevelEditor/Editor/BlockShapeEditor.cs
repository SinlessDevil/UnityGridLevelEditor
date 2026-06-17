using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// A small NxN paint grid for defining a block's footprint as cell offsets
    /// from the center. Click a cell to toggle it on/off.
    /// </summary>
    public class BlockShapeEditor : VisualElement
    {
        private const int Size = 5;
        private const int Center = Size / 2;

        private readonly VisualElement[,] _cells = new VisualElement[Size, Size];
        private readonly HashSet<Vector2Int> _offsets = new();

        public BlockShapeEditor()
        {
            AddToClassList("le-shape");

            for (int gy = 0; gy < Size; gy++)
            {
                var row = new VisualElement();
                row.AddToClassList("le-shape__row");

                for (int gx = 0; gx < Size; gx++)
                {
                    var cell = new VisualElement();
                    cell.AddToClassList("le-shape-cell");
                    if (gx == Center && gy == Center)
                        cell.AddToClassList("le-shape-cell--center");

                    int x = gx, y = gy;
                    cell.RegisterCallback<PointerDownEvent>(_ => Toggle(x, y));

                    _cells[gx, gy] = cell;
                    row.Add(cell);
                }

                Add(row);
            }

            // Default footprint: just the center (a 1x1 block).
            _offsets.Add(Vector2Int.zero);
            Refresh();
        }

        public void SetOffsets(IEnumerable<Vector2Int> offsets)
        {
            _offsets.Clear();
            if (offsets != null)
            {
                foreach (var o in offsets)
                {
                    // Ignore anything outside the editable grid.
                    if (Mathf.Abs(o.x) <= Center && Mathf.Abs(o.y) <= Center)
                        _offsets.Add(o);
                }
            }

            if (_offsets.Count == 0)
                _offsets.Add(Vector2Int.zero);

            Refresh();
        }

        public List<Vector2Int> GetOffsets() => new(_offsets);

        private void Toggle(int gx, int gy)
        {
            var offset = new Vector2Int(gx - Center, gy - Center);

            if (!_offsets.Remove(offset))
                _offsets.Add(offset);

            // Never allow an empty shape.
            if (_offsets.Count == 0)
                _offsets.Add(Vector2Int.zero);

            Refresh();
        }

        private void Refresh()
        {
            for (int gy = 0; gy < Size; gy++)
            for (int gx = 0; gx < Size; gx++)
            {
                var offset = new Vector2Int(gx - Center, gy - Center);
                _cells[gx, gy].EnableInClassList("le-shape-cell--on", _offsets.Contains(offset));
            }
        }
    }
}
