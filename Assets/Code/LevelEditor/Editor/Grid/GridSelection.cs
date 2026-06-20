using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// The set of currently selected cells plus the pending range anchor
    /// (<see cref="Start"/>). Exposes a List-like surface so it can stand in for the
    /// old <c>_selection</c> field and satisfy the public <c>Selection</c> property.
    /// </summary>
    internal sealed class GridSelection : IReadOnlyList<Vector2Int>
    {
        private readonly List<Vector2Int> _cells = new();

        /// <summary>Anchor of an in-progress Ctrl+click range selection (null when none).</summary>
        public Vector2Int? Start;

        public int Count => _cells.Count;
        public Vector2Int this[int index] => _cells[index];

        public IEnumerator<Vector2Int> GetEnumerator() => _cells.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _cells.GetEnumerator();

        public bool Contains(Vector2Int p) => _cells.Contains(p);
        public void Add(Vector2Int p) => _cells.Add(p);
        public void AddRange(IEnumerable<Vector2Int> cells) => _cells.AddRange(cells);
        public void Clear() => _cells.Clear();

        /// <summary>Replaces the selection with the inclusive rectangle spanned by two corners.</summary>
        public void SelectRange(Vector2Int start, Vector2Int end)
        {
            _cells.Clear();

            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int minY = Mathf.Min(start.y, end.y);
            int maxY = Mathf.Max(start.y, end.y);

            for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
                _cells.Add(new Vector2Int(x, y));
        }
    }
}
