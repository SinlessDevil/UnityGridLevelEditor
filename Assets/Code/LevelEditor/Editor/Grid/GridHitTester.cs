using UnityEngine;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// Maps a panel-space point to a grid cell. Cells form a uniform grid, so the hit
    /// is found by arithmetic (O(1)); it falls back to a full scan while the layout
    /// hasn't resolved yet (zero/invalid bounds).
    /// </summary>
    internal sealed class GridHitTester
    {
        private readonly GridContext _ctx;

        public GridHitTester(GridContext ctx) => _ctx = ctx;

        public bool TryGetCellAt(Vector2 panelPosition, out Vector2Int cellPos)
        {
            cellPos = default;

            var cells = _ctx.Cells;
            if (cells == null || _ctx.Level == null)
                return false;

            var origin = cells[0, 0].worldBound;
            float stepX = _ctx.Width > 1 ? cells[1, 0].worldBound.xMin - origin.xMin : origin.width;
            float stepY = _ctx.Height > 1 ? cells[0, 1].worldBound.yMin - origin.yMin : origin.height;

            // Fall back to a scan if the layout hasn't resolved yet (zero/invalid bounds).
            if (stepX <= 0f || stepY <= 0f)
                return ScanForCell(panelPosition, out cellPos);

            int col = Mathf.FloorToInt((panelPosition.x - origin.xMin) / stepX);
            int row = Mathf.FloorToInt((panelPosition.y - origin.yMin) / stepY);

            if (col < 0 || col >= _ctx.Width || row < 0 || row >= _ctx.Height)
                return false;

            // Verify the hit so points in the gap between cells behave like before.
            if (!cells[col, row].worldBound.Contains(panelPosition))
                return false;

            cellPos = new Vector2Int(col, row);
            return true;
        }

        private bool ScanForCell(Vector2 panelPosition, out Vector2Int cellPos)
        {
            cellPos = default;

            var cells = _ctx.Cells;
            for (int y = 0; y < _ctx.Height; y++)
            for (int x = 0; x < _ctx.Width; x++)
            {
                if (cells[x, y].worldBound.Contains(panelPosition))
                {
                    cellPos = new Vector2Int(x, y);
                    return true;
                }
            }

            return false;
        }
    }
}
