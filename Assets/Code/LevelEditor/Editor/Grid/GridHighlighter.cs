using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// Owns transient cell decorations: drop-target / drop-invalid hover highlights and
    /// the "no space here" flash. Reads the live cell array from <see cref="GridContext"/>;
    /// the flash is scheduled on a host <see cref="VisualElement"/> (the grid view).
    /// </summary>
    internal sealed class GridHighlighter
    {
        private readonly GridContext _ctx;
        private readonly VisualElement _scheduler;
        private readonly List<Vector2Int> _hoverCells = new();

        public GridHighlighter(GridContext ctx, VisualElement scheduler)
        {
            _ctx = ctx;
            _scheduler = scheduler;
        }

        /// <summary>Clears the tracked hover cells without touching visuals (used on rebuild).</summary>
        public void ResetState() => _hoverCells.Clear();

        public void SetHoverCell(Vector2Int? cell)
        {
            if (cell.HasValue)
                SetHoverCells(new[] { cell.Value }, true);
            else
                SetHoverCells(Array.Empty<Vector2Int>(), true);
        }

        public void SetHoverCells(IEnumerable<Vector2Int> cells, bool valid)
        {
            var grid = _ctx.Cells;

            foreach (var c in _hoverCells)
            {
                if (!_ctx.IsValid(c))
                    continue;

                grid[c.x, c.y].RemoveFromClassList("le-cell--drop-target");
                grid[c.x, c.y].RemoveFromClassList("le-cell--drop-invalid");
            }

            _hoverCells.Clear();

            string cssClass = valid ? "le-cell--drop-target" : "le-cell--drop-invalid";
            foreach (var c in cells)
            {
                if (!_ctx.IsValid(c))
                    continue;

                _ctx.Cells[c.x, c.y].AddToClassList(cssClass);
                _hoverCells.Add(c);
            }
        }

        /// <summary>Briefly blinks the given cells red (used to signal "no space here").</summary>
        public void FlashCells(IEnumerable<Vector2Int> cells)
        {
            var targets = new List<VisualElement>();
            foreach (var c in cells)
                if (_ctx.IsValid(c))
                    targets.Add(_ctx.Cells[c.x, c.y]);

            if (targets.Count == 0)
                return;

            const string flashClass = "le-cell--flash";
            const int toggles = 6;
            int count = 0;

            IVisualElementScheduledItem item = null;
            item = _scheduler.schedule.Execute(() =>
            {
                bool on = count % 2 == 0;
                foreach (var t in targets)
                    t.EnableInClassList(flashClass, on);

                if (++count >= toggles)
                {
                    foreach (var t in targets)
                        t.RemoveFromClassList(flashClass);
                    item?.Pause();
                }
            }).Every(110);
        }
    }
}
